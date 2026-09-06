using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

public sealed record ArchiveActionResult(ArchiveOperationStatus Status, ArchiveOperation Operation, string? Error = null);

/// <summary>Serial, local-only storage transitions. The database is changed only after the final file is verified.</summary>
public sealed class InstallerArchiveService(IScanCatalogRepository repository, IPortablePathResolver paths, IArchiveLocationResolver locations, IFileHashCalculator hashes, IAppLogger? logger = null, IArchiveFileSystem? archiveFileSystem = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IArchiveFileSystem fileSystem = archiveFileSystem ?? new SystemArchiveFileSystem();
    public Task<ArchiveActionResult> ArchiveAsync(InstallerFile file, CancellationToken token, IProgress<ArchiveProgress>? progress = null) => MoveAsync(file, InstallerStorageState.Archived, null, token, progress);
    public Task<ArchiveActionResult> TrashAsync(InstallerFile file, CancellationToken token, IProgress<ArchiveProgress>? progress = null) => MoveAsync(file, InstallerStorageState.Trashed, null, token, progress);
    public Task<ArchiveActionResult> RestoreAsync(InstallerFile file, string? alternateDestination, CancellationToken token, IProgress<ArchiveProgress>? progress = null) => MoveAsync(file, InstallerStorageState.Active, alternateDestination, token, progress);
    public async Task<ArchiveActionResult> PurgeAsync(InstallerFile file, CancellationToken token)
    {
        if (file.StorageState != InstallerStorageState.Trashed) return await FinishAsync(file, ArchiveOperationType.Purge, null, null, ArchiveOperationStatus.Error, "Постоянное удаление разрешено только из корзины.", token);
        if (file.IsPinned) return await FinishAsync(file, ArchiveOperationType.Purge, null, null, ArchiveOperationStatus.Error, "Закреплённый файл сначала необходимо открепить.", token);
        await _gate.WaitAsync(token); try
        {
            var source = await ResolvePathAsync(file, token); var op = NewOperation(file, ArchiveOperationType.Purge, source, null, null);
            await repository.SaveArchiveOperationAsync(op, token);
            try { fileSystem.Delete(source); await repository.MarkInstallerPurgedAsync(file.Id, token); return await CompleteAsync(op, ArchiveOperationStatus.Completed, null, token); }
            catch (Exception ex) { return await CompleteAsync(op, ArchiveOperationStatus.Error, ex.Message, token); }
        } finally { _gate.Release(); }
    }
    private async Task<ArchiveActionResult> MoveAsync(InstallerFile file, InstallerStorageState state, string? alternateDestination, CancellationToken token, IProgress<ArchiveProgress>? progress)
    {
        if (file.IsPinned && state != InstallerStorageState.Active) return await FinishAsync(file, state == InstallerStorageState.Archived ? ArchiveOperationType.Archive : ArchiveOperationType.MoveToTrash, null, null, ArchiveOperationStatus.Error, "Закреплённый файл сначала необходимо открепить.", token);
        if (!file.Exists) return await FinishAsync(file, ArchiveOperationType.Archive, null, null, ArchiveOperationStatus.Error, "Исходный файл не найден в каталоге.", token);
        await _gate.WaitAsync(token); try
        {
            var source = await ResolvePathAsync(file, token); var type = state == InstallerStorageState.Archived ? ArchiveOperationType.Archive : state == InstallerStorageState.Trashed ? ArchiveOperationType.MoveToTrash : file.StorageState == InstallerStorageState.Trashed ? ArchiveOperationType.RestoreFromTrash : ArchiveOperationType.Restore;
            progress?.Report(new(file.FileName, type, 0, 1, 0, file.Size, ArchiveOperationStatus.Running));
            if (!fileSystem.Exists(source)) return await FinishAsync(file, type, source, null, ArchiveOperationStatus.Error, "Исходный файл исчез до выполнения операции.", token);
            if (IsTransient(source)) return await FinishAsync(file, type, source, null, ArchiveOperationStatus.Error, "Временный или незавершённый файл нельзя перемещать в архив.", token);
            var destination = await GetDestinationAsync(file, state, alternateDestination, token); var op = NewOperation(file, type, source, destination, file.Sha256);
            await repository.SaveArchiveOperationAsync(op, token);
            var finalized = false; var databaseUpdated = false; string? sourceHash = null;
            try
            {
                sourceHash = file.Sha256 ?? await hashes.ComputeSha256Async(source, token);
                if (fileSystem.Exists(destination)) { var existing = await hashes.ComputeSha256Async(destination, token); return await CompleteAsync(op with { Sha256 = sourceHash }, string.Equals(existing, sourceHash, StringComparison.OrdinalIgnoreCase) ? ArchiveOperationStatus.AlreadyExists : ArchiveOperationStatus.Conflict, null, token); }
                fileSystem.CreateDirectory(Path.GetDirectoryName(destination)!); var part = destination + ".archive-part";
                try
                {
                    await fileSystem.CopyAsync(source, part, new Progress<long>(bytes => progress?.Report(new(file.FileName, type, 0, 1, bytes, file.Size, ArchiveOperationStatus.Running))), token);
                    if (fileSystem.GetLength(source) != fileSystem.GetLength(part) || !string.Equals(sourceHash, await hashes.ComputeSha256Async(part, token), StringComparison.OrdinalIgnoreCase)) throw new IOException("Проверка SHA-256 после копирования не пройдена.");
                    fileSystem.Move(part, destination); finalized = true;
                    var root = state == InstallerStorageState.Active ? await GetRestoreRootAsync(file, destination, alternateDestination, token) : await GetManagedRootAsync(state, token); var relative = Path.GetRelativePath(paths.Resolve(root), destination);
                    if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)) throw new InvalidOperationException("Путь восстановления находится вне выбранного user root.");
                    var originalRoot = file.OriginalScanRootId ?? (file.StorageState == InstallerStorageState.Active ? file.ScanRootId : null);
                    var originalPath = file.OriginalRelativePath ?? (file.StorageState == InstallerStorageState.Active ? file.RelativePath : null);
                    if (state == InstallerStorageState.Active) { originalRoot = null; originalPath = null; }
                    await repository.UpdateInstallerStorageAsync(new(file.Id, root.Id, relative, state, sourceHash, originalRoot, originalPath, DateTimeOffset.UtcNow), token);
                    databaseUpdated = true;
                    fileSystem.Delete(source);
                    progress?.Report(new(file.FileName, type, 1, 1, file.Size, file.Size, ArchiveOperationStatus.Completed));
                    logger?.Information("archive", $"operation={type} installerId={file.Id} status=completed");
                    return await CompleteAsync(op with { Sha256 = sourceHash }, ArchiveOperationStatus.Completed, null, token);
                }
                finally
                {
                    try { if (fileSystem.Exists(part)) fileSystem.Delete(part); }
                    catch (Exception cleanupError) { logger?.Error("archive", $"operation={type} installerId={file.Id} temporary-cleanup-error={cleanupError.Message}"); }
                }
            }
            catch (OperationCanceledException) { await RollbackAsync(); return await CompleteAsync(op, ArchiveOperationStatus.Cancelled, null, CancellationToken.None); }
            catch (Exception ex) { await RollbackAsync(); logger?.Error("archive", $"operation={type} installerId={file.Id} error={ex.Message}"); return await CompleteAsync(op, ArchiveOperationStatus.Error, ex.Message, CancellationToken.None); }
            async Task RollbackAsync()
            {
                try
                {
                    if (databaseUpdated)
                        await repository.UpdateInstallerStorageAsync(new(file.Id, file.ScanRootId, file.RelativePath, file.StorageState, file.Sha256, file.OriginalScanRootId, file.OriginalRelativePath, DateTimeOffset.UtcNow), CancellationToken.None);
                    if (finalized && fileSystem.Exists(destination) && fileSystem.Exists(source)) fileSystem.Delete(destination);
                }
                catch (Exception rollbackError)
                {
                    logger?.Error("archive", $"operation={type} installerId={file.Id} rollback-error={rollbackError.Message}");
                }
            }
        } finally { _gate.Release(); }
    }
    private async Task<string> ResolvePathAsync(InstallerFile file, CancellationToken token) { var root = (await repository.GetScanRootsAsync(token)).Single(x => x.Id == file.ScanRootId); return Path.GetFullPath(Path.Combine(paths.Resolve(root), file.RelativePath)); }
    private async Task<ScanRoot> GetManagedRootAsync(InstallerStorageState state, CancellationToken token) => state switch { InstallerStorageState.Archived => await repository.EnsureManagedScanRootAsync(ScanRootRole.Archive, locations.ArchiveStoredPath, locations.PathKind, token), InstallerStorageState.Trashed => await repository.EnsureManagedScanRootAsync(ScanRootRole.Trash, locations.PathKind == ScanRootPathKind.Absolute ? locations.TrashRoot : Path.Combine(locations.ArchiveStoredPath, "Trash"), locations.PathKind, token), _ => throw new InvalidOperationException("Managed root requested for active storage.") };
    private async Task<ScanRoot> GetRestoreRootAsync(InstallerFile file, string destination, string? alternate, CancellationToken token)
    {
        var roots = await repository.GetScanRootsAsync(token);
        if (string.IsNullOrWhiteSpace(alternate)) return roots.Single(x => x.Id == file.OriginalScanRootId && x.Role == ScanRootRole.User);
        var match = roots.Where(x => x.Role == ScanRootRole.User).Select(x => (Root: x, Full: paths.Resolve(x))).FirstOrDefault(x => IsInside(x.Full, destination)).Root;
        return match ?? await repository.AddScanRootAsync(Path.GetDirectoryName(destination)!, ScanRootPathKind.Absolute, true, token);
    }
    private static bool IsInside(string root, string path) { var relative = Path.GetRelativePath(root, path); return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal); }
    private async Task<string> GetDestinationAsync(InstallerFile file, InstallerStorageState state, string? alternate, CancellationToken token)
    {
        if (state == InstallerStorageState.Archived) return Path.Combine(locations.ArchiveRoot, Safe(file.ProductName ?? "unknown") + "-" + (file.ProductId?.ToString("N")[..8] ?? "unlinked"), Safe(file.NormalizedVersion ?? file.ProductVersion ?? "unknown"), Safe(file.FileName));
        if (state == InstallerStorageState.Trashed) return Path.Combine(locations.TrashRoot, file.Id.ToString(), Safe(file.FileName));
        if (!string.IsNullOrWhiteSpace(alternate)) return Path.GetFullPath(alternate);
        if (file.OriginalScanRootId is null || string.IsNullOrWhiteSpace(file.OriginalRelativePath)) throw new InvalidOperationException("Для восстановления требуется выбрать папку назначения.");
        var root = (await repository.GetScanRootsAsync(token)).Single(x => x.Id == file.OriginalScanRootId); return Path.Combine(paths.Resolve(root), file.OriginalRelativePath);
    }
    private static string Safe(string value) { var invalid = Path.GetInvalidFileNameChars(); var cleaned = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(); return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned[..Math.Min(100, cleaned.Length)]; }
    private static bool IsTransient(string path) => path.EndsWith(".part", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".archive-part", StringComparison.OrdinalIgnoreCase) || path.Contains($"{Path.DirectorySeparatorChar}Cache{Path.DirectorySeparatorChar}Staging{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    private static ArchiveOperation NewOperation(InstallerFile file, ArchiveOperationType type, string source, string? destination, string? sha) => new(Guid.NewGuid(),file.Id,file.ProductId,type,file.StorageState,type == ArchiveOperationType.Purge ? null : type is ArchiveOperationType.Archive ? InstallerStorageState.Archived : type == ArchiveOperationType.MoveToTrash ? InstallerStorageState.Trashed : InstallerStorageState.Active,source,destination,sha,ArchiveOperationStatus.Running,null,DateTimeOffset.UtcNow,null);
    private async Task<ArchiveActionResult> FinishAsync(InstallerFile file, ArchiveOperationType type, string? source, string? destination, ArchiveOperationStatus status, string? error, CancellationToken token) { var op=NewOperation(file,type,source ?? "",destination,file.Sha256) with { Status=status,Error=error,CompletedUtc=DateTimeOffset.UtcNow }; await repository.SaveArchiveOperationAsync(op, token); return new(status,op,error); }
    private async Task<ArchiveActionResult> CompleteAsync(ArchiveOperation op, ArchiveOperationStatus status, string? error, CancellationToken token) { var done=op with { Status=status,Error=error,CompletedUtc=DateTimeOffset.UtcNow }; await repository.SaveArchiveOperationAsync(done, token); return new(status,done,error); }
}

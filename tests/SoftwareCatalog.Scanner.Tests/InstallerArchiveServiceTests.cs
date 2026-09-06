using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Scanner;

namespace SoftwareCatalog.Scanner.Tests;

public sealed class InstallerArchiveServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "catalog-archive-" + Guid.NewGuid().ToString("N"));
    public InstallerArchiveServiceTests() => Directory.CreateDirectory(_folder);
    [Fact] public async Task ArchiveRestoreTrashAndPurgePreserveLogicalInstaller()
    {
        var source = Path.Combine(_folder,"source"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content");
        var repo = new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var file=FileRecord(); repo.Files.Add(file);
        var service = Service(repo); var archived=await service.ArchiveAsync(file,CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Completed,archived.Status); Assert.False(File.Exists(path)); var afterArchive=Assert.Single(repo.Files); Assert.Equal(file.Id,afterArchive.Id); Assert.Equal(file.ProductId,afterArchive.ProductId); Assert.Equal(InstallerStorageState.Archived,afterArchive.StorageState); Assert.NotEmpty(repo.Operations);
        var restored=await service.RestoreAsync(afterArchive,null,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Completed,restored.Status); Assert.True(File.Exists(path)); var active=Assert.Single(repo.Files); Assert.Equal(InstallerStorageState.Active,active.StorageState);
        var trashed=await service.TrashAsync(active,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Completed,trashed.Status); var trash=Assert.Single(repo.Files); Assert.Equal(InstallerStorageState.Trashed,trash.StorageState);
        var purge=await service.PurgeAsync(trash,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Completed,purge.Status); Assert.False(Assert.Single(repo.Files).Exists); Assert.Contains(repo.Operations,x=>x.OperationType==ArchiveOperationType.Purge && x.Status==ArchiveOperationStatus.Completed);
    }
    [Fact] public async Task PinnedFileIsNeverMovedAndCollisionNeverOverwrites()
    {
        var source=Path.Combine(_folder,"source2"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content"); var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var pinned=FileRecord() with { IsPinned=true }; repo.Files.Add(pinned);
        var service=Service(repo); var blocked=await service.ArchiveAsync(pinned,CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Error,blocked.Status); Assert.True(File.Exists(path));
        repo.Files[0]=pinned with { IsPinned=false }; var archiveDir=Path.Combine(_folder,"Archive","Tool-11111111","1.0"); Directory.CreateDirectory(archiveDir); var collision=Path.Combine(archiveDir,"tool.exe"); await File.WriteAllTextAsync(collision,"different"); var result=await service.ArchiveAsync(repo.Files[0],CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Conflict,result.Status); Assert.Equal("different",await File.ReadAllTextAsync(collision)); Assert.True(File.Exists(path));
    }
    [Fact] public async Task PinnedArchivedFileCanBeRestoredToAlternateUserRoot()
    {
        var source=Path.Combine(_folder,"source3"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content"); var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var file=FileRecord(); repo.Files.Add(file); var service=Service(repo); Assert.Equal(ArchiveOperationStatus.Completed,(await service.ArchiveAsync(file,CancellationToken.None)).Status);
        var archived=repo.Files.Single() with { IsPinned=true }; repo.Files[0]=archived; var alternate=Path.Combine(_folder,"alternate","tool.exe"); var restored=await service.RestoreAsync(archived,alternate,CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Completed,restored.Status); var active=repo.Files.Single(); Assert.Equal(InstallerStorageState.Active,active.StorageState); Assert.True(active.IsPinned); Assert.Equal(file.Id,active.Id); Assert.Equal(file.ProductId,active.ProductId); Assert.True(File.Exists(alternate)); Assert.Equal(ScanRootRole.User,(await repo.GetScanRootsAsync(CancellationToken.None)).Single(x=>x.Id==active.ScanRootId).Role);
    }
    [Fact] public async Task DatabaseFailureAfterFinalizeRollsBackDestination()
    {
        var source=Path.Combine(_folder,"source4"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content"); var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)) { FailStorageUpdate=true }; var file=FileRecord(); repo.Files.Add(file);
        var result=await Service(repo).ArchiveAsync(file,CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Error,result.Status); Assert.True(File.Exists(path)); Assert.Equal(InstallerStorageState.Active,repo.Files.Single().StorageState); Assert.Empty(Directory.Exists(Path.Combine(_folder,"Archive")) ? Directory.GetFiles(Path.Combine(_folder,"Archive"),"*",SearchOption.AllDirectories) : []);
    }
    [Fact] public async Task CopyFinalizeAndCancellationFailuresLeaveNoDestinationOrStateChange()
    {
        foreach (var failure in new[] { Failure.Copy, Failure.Move, Failure.CancelCopy })
        {
            var source = Path.Combine(_folder, failure.ToString()); Directory.CreateDirectory(source); var path = Path.Combine(source, "tool.exe"); await File.WriteAllTextAsync(path, "content");
            var repo = new Repo(new ScanRoot(1, source, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)); var file = FileRecord(); repo.Files.Add(file);
            var result = await Service(repo, new FailingFileSystem(failure)).ArchiveAsync(file, CancellationToken.None);
            Assert.Equal(failure == Failure.CancelCopy ? ArchiveOperationStatus.Cancelled : ArchiveOperationStatus.Error, result.Status);
            Assert.True(File.Exists(path)); Assert.Equal(InstallerStorageState.Active, repo.Files.Single().StorageState);
            Assert.Empty(Directory.Exists(Path.Combine(_folder, "Archive")) ? Directory.GetFiles(Path.Combine(_folder, "Archive"), "*", SearchOption.AllDirectories) : []);
        }
    }
    [Fact] public async Task SourceDeleteFailureRollsBackDatabaseAndDestination()
    {
        var source=Path.Combine(_folder,"source-delete-failure"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllTextAsync(path,"content");
        var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var file=FileRecord(); repo.Files.Add(file);
        var result=await Service(repo,new FailingFileSystem(Failure.DeleteSource)).ArchiveAsync(file,CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Error,result.Status); Assert.True(File.Exists(path)); Assert.Equal(InstallerStorageState.Active,repo.Files.Single().StorageState);
        Assert.Empty(Directory.Exists(Path.Combine(_folder,"Archive")) ? Directory.GetFiles(Path.Combine(_folder,"Archive"),"*",SearchOption.AllDirectories) : []);
    }
    [Fact] public async Task ArchiveReportsByteProgress()
    {
        var source=Path.Combine(_folder,"progress"); Directory.CreateDirectory(source); var path=Path.Combine(source,"tool.exe"); await File.WriteAllBytesAsync(path,new byte[150_000]);
        var repo=new Repo(new ScanRoot(1,source,ScanRootPathKind.Absolute,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow)); var file=FileRecord() with { Size=150_000 }; repo.Files.Add(file); var reported = new List<ArchiveProgress>();
        var result=await Service(repo).ArchiveAsync(file,CancellationToken.None,new Progress<ArchiveProgress>(reported.Add));
        Assert.Equal(ArchiveOperationStatus.Completed,result.Status); Assert.Contains(reported, item => item.BytesProcessed > 0 && item.Status == ArchiveOperationStatus.Running); Assert.Equal(150_000,reported.Last().BytesProcessed);
    }
    [Fact] public async Task SourceDisappearanceAndPurgeGuardsAreRecordedWithoutChangingState()
    {
        var source = Path.Combine(_folder, "guards"); Directory.CreateDirectory(source); var path = Path.Combine(source, "tool.exe"); await File.WriteAllTextAsync(path, "content"); var repo = new Repo(new ScanRoot(1, source, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)); var active = FileRecord(); repo.Files.Add(active); var service = Service(repo);
        File.Delete(path); var missing = await service.ArchiveAsync(active, CancellationToken.None); Assert.Equal(ArchiveOperationStatus.Error, missing.Status); Assert.Equal(InstallerStorageState.Active, repo.Files.Single().StorageState); Assert.Equal(ArchiveOperationStatus.Error, repo.Operations.Last().Status);
        Assert.Equal(ArchiveOperationStatus.Error, (await service.PurgeAsync(active, CancellationToken.None)).Status);
        repo.Files[0] = active with { StorageState = InstallerStorageState.Archived }; Assert.Equal(ArchiveOperationStatus.Error, (await service.PurgeAsync(repo.Files.Single(), CancellationToken.None)).Status);
        repo.Files[0] = active with { StorageState = InstallerStorageState.Trashed, IsPinned = true }; Assert.Equal(ArchiveOperationStatus.Error, (await service.PurgeAsync(repo.Files.Single(), CancellationToken.None)).Status);
    }
    [Fact] public async Task TrashRestoreLifecyclePreservesIdentityMetadataAndHash()
    {
        var source = Path.Combine(_folder, "trash-restore"); Directory.CreateDirectory(source); var path = Path.Combine(source, "tool.exe"); await File.WriteAllTextAsync(path, "content"); var repo = new Repo(new ScanRoot(1, source, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)); var original = FileRecord() with { Architecture = "x64", MetadataError = "metadata" }; repo.Files.Add(original); var service = Service(repo);
        Assert.Equal(ArchiveOperationStatus.Completed, (await service.TrashAsync(original, CancellationToken.None)).Status); var trashed = repo.Files.Single(); Assert.Equal(ArchiveOperationStatus.Completed, (await service.RestoreAsync(trashed, null, CancellationToken.None)).Status); var restored = repo.Files.Single();
        Assert.Equal(original.Id, restored.Id); Assert.Equal(original.ProductId, restored.ProductId); Assert.Equal(trashed.Sha256, restored.Sha256); Assert.NotNull(restored.Sha256); Assert.Equal(original.Architecture, restored.Architecture); Assert.Equal(original.MetadataError, restored.MetadataError); Assert.Equal(InstallerStorageState.Active, restored.StorageState); Assert.True(File.Exists(path));
    }
    [Fact] public async Task HashMismatchRemovesTemporaryCopyAndKeepsSource()
    {
        var source = Path.Combine(_folder, "hash-mismatch"); Directory.CreateDirectory(source); var path = Path.Combine(source, "tool.exe"); await File.WriteAllTextAsync(path, "content"); var repo = new Repo(new ScanRoot(1, source, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)); var file = FileRecord(); repo.Files.Add(file);
        var result = await Service(repo, null, new MismatchingHashCalculator()).ArchiveAsync(file, CancellationToken.None);
        Assert.Equal(ArchiveOperationStatus.Error, result.Status); Assert.True(File.Exists(path)); Assert.Equal(InstallerStorageState.Active, repo.Files.Single().StorageState); Assert.Empty(Directory.Exists(Path.Combine(_folder, "Archive")) ? Directory.GetFiles(Path.Combine(_folder, "Archive"), "*", SearchOption.AllDirectories) : []);
    }
    [Fact] public async Task QueuedCancellationNeverStartsSecondFilesystemOperation()
    {
        var source = Path.Combine(_folder, "queued"); Directory.CreateDirectory(source); await File.WriteAllTextAsync(Path.Combine(source, "one.exe"), "one"); await File.WriteAllTextAsync(Path.Combine(source, "two.exe"), "two");
        var repo = new Repo(new ScanRoot(1, source, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)); var first = FileRecord() with { RelativePath="one.exe", FileName="one.exe" }; var second = FileRecord() with { Id=43, RelativePath="two.exe", FileName="two.exe" }; repo.Files.AddRange([first, second]); var fs = new BlockingFileSystem(); var service = Service(repo, fs);
        var running = service.ArchiveAsync(first, CancellationToken.None); await fs.Started.Task; using var cancelled = new CancellationTokenSource(); var queued = service.ArchiveAsync(second, cancelled.Token); cancelled.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.DoesNotContain(repo.Operations, operation => operation.InstallerId == second.Id); Assert.Equal(InstallerStorageState.Active, repo.Files.Single(file => file.Id == second.Id).StorageState); Assert.True(File.Exists(Path.Combine(source, "two.exe")));
        fs.Release.TrySetResult(); Assert.Equal(ArchiveOperationStatus.Completed, (await running).Status);
    }
    [Theory]
    [InlineData("archive", "content", ArchiveOperationStatus.AlreadyExists)]
    [InlineData("archive", "different", ArchiveOperationStatus.Conflict)]
    [InlineData("trash", "content", ArchiveOperationStatus.AlreadyExists)]
    [InlineData("trash", "different", ArchiveOperationStatus.Conflict)]
    [InlineData("restore", "content", ArchiveOperationStatus.AlreadyExists)]
    [InlineData("restore", "different", ArchiveOperationStatus.Conflict)]
    public async Task CollisionsNeverOverwriteOrRelocateInstaller(string operation, string existingContent, ArchiveOperationStatus expected)
    {
        var source = Path.Combine(_folder, "collision-" + operation + "-" + existingContent); Directory.CreateDirectory(source); var originalPath = Path.Combine(source, "tool.exe"); await File.WriteAllTextAsync(originalPath, "content"); var repo = new Repo(new ScanRoot(1, source, ScanRootPathKind.Absolute, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)); var file = FileRecord(); repo.Files.Add(file); var service = Service(repo);
        ArchiveActionResult result;
        if (operation == "archive")
        {
            var destination = Path.Combine(_folder, "Archive", "Tool-11111111", "1.0", "tool.exe"); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); await File.WriteAllTextAsync(destination, existingContent); result = await service.ArchiveAsync(file, CancellationToken.None); Assert.True(File.Exists(originalPath));
        }
        else if (operation == "trash")
        {
            var destination = Path.Combine(_folder, "Archive", "Trash", "42", "tool.exe"); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); await File.WriteAllTextAsync(destination, existingContent); result = await service.TrashAsync(file, CancellationToken.None); Assert.True(File.Exists(originalPath));
        }
        else
        {
            Assert.Equal(ArchiveOperationStatus.Completed, (await service.ArchiveAsync(file, CancellationToken.None)).Status); var archived = repo.Files.Single(); Directory.CreateDirectory(source); await File.WriteAllTextAsync(originalPath, existingContent); result = await service.RestoreAsync(archived, null, CancellationToken.None); Assert.Equal(InstallerStorageState.Archived, repo.Files.Single().StorageState);
        }
        Assert.Equal(expected, result.Status); Assert.Equal(existingContent, await File.ReadAllTextAsync(result.Operation.DestinationPath!));
    }
    private InstallerArchiveService Service(Repo repo, IArchiveFileSystem? fileSystem = null, IFileHashCalculator? hashes = null) => new(repo,new Resolver(),new Locations(Path.Combine(_folder,"Archive")),hashes ?? new FileHashCalculator(), null, fileSystem);
    private static InstallerFile FileRecord() { var now=DateTimeOffset.UtcNow; return new InstallerFile(42,1,"tool.exe","tool.exe",".exe",7,now,null,now,now,true,ProductName:"Tool",ProductVersion:"1.0",NormalizedVersion:"1.0",ProductId:Guid.Parse("11111111-1111-1111-1111-111111111111")); }
    public void Dispose() { if(Directory.Exists(_folder)) Directory.Delete(_folder,true); }
    private sealed class Resolver : IPortablePathResolver { public string Resolve(ScanRoot root)=>root.StoredPath; public string ToStoredPath(string path,ScanRootPathKind kind)=>path; public string GetRelativePath(ScanRoot root,string path)=>Path.GetRelativePath(root.StoredPath,path); public ScanRootAvailability GetAvailability(ScanRoot root)=>ScanRootAvailability.Available; }
    private sealed class Locations(string root) : IArchiveLocationResolver { public string ArchiveRoot=>root; public string TrashRoot=>Path.Combine(root,"Trash"); public string ArchiveStoredPath=>root; public ScanRootPathKind PathKind=>ScanRootPathKind.Absolute; }
    private enum Failure { Copy, Move, CancelCopy, DeleteSource }
    private sealed class FailingFileSystem(Failure failure) : IArchiveFileSystem
    {
        private readonly SystemArchiveFileSystem _inner = new();
        private bool _deleteFailed;
        public bool Exists(string path) => _inner.Exists(path);
        public long GetLength(string path) => _inner.GetLength(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public Task CopyAsync(string source, string destination, IProgress<long>? progress, CancellationToken token)
        {
            if (failure == Failure.Copy) throw new IOException("copy failure");
            if (failure == Failure.CancelCopy) throw new OperationCanceledException(token);
            return _inner.CopyAsync(source, destination, progress, token);
        }
        public void Move(string source, string destination) { if (failure == Failure.Move) throw new IOException("finalize failure"); _inner.Move(source, destination); }
        public void Delete(string path) { if (failure == Failure.DeleteSource && !_deleteFailed) { _deleteFailed = true; throw new IOException("source delete failure"); } _inner.Delete(path); }
    }
    private sealed class MismatchingHashCalculator : IFileHashCalculator
    {
        public Task<string> ComputeSha256Async(string path, CancellationToken token) => Task.FromResult(path.EndsWith(".archive-part", StringComparison.OrdinalIgnoreCase) ? "B" : "A");
    }
    private sealed class BlockingFileSystem : IArchiveFileSystem
    {
        private readonly SystemArchiveFileSystem _inner = new(); public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Exists(string path) => _inner.Exists(path); public long GetLength(string path) => _inner.GetLength(path); public void CreateDirectory(string path) => _inner.CreateDirectory(path); public void Move(string source, string destination) => _inner.Move(source, destination); public void Delete(string path) => _inner.Delete(path);
        public async Task CopyAsync(string source, string destination, IProgress<long>? progress, CancellationToken token) { await _inner.CopyAsync(source, destination, progress, token); Started.TrySetResult(); await Release.Task.WaitAsync(token); }
    }
    private sealed class Repo(ScanRoot root) : IScanCatalogRepository
    {
        private readonly List<ScanRoot> _roots=[root]; public List<InstallerFile> Files { get; }=[]; public List<ArchiveOperation> Operations { get; }=[]; public bool FailStorageUpdate { get; set; }
        public Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<ScanRoot>>(_roots); public Task<ScanRoot> AddScanRootAsync(string p,ScanRootPathKind k,bool i,CancellationToken t) { var added=new ScanRoot(_roots.Count+1,p,k,i,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow);_roots.Add(added);return Task.FromResult(added); } public Task UpdateScanRootAsync(long i,string p,ScanRootPathKind k,CancellationToken t)=>Task.CompletedTask; public Task RemoveScanRootAsync(long i,CancellationToken t)=>Task.CompletedTask; public Task<InstallerFile?> FindInstallerAsync(long r,string p,CancellationToken t)=>Task.FromResult(Files.SingleOrDefault(x=>x.ScanRootId==r&&x.RelativePath==p)); public Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> f,CancellationToken t)=>Task.CompletedTask; public Task MarkMissingAsync(long r,DateTimeOffset s,CancellationToken t)=>Task.CompletedTask; public Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>(Files);
        public Task<ScanRoot> EnsureManagedScanRootAsync(ScanRootRole role,string path,ScanRootPathKind kind,CancellationToken t) { var old=_roots.SingleOrDefault(x=>x.Role==role); if(old is not null)return Task.FromResult(old); var next=new ScanRoot(_roots.Count+1,path,kind,true,true,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,role);_roots.Add(next);return Task.FromResult(next); }
        public Task UpdateInstallerStorageAsync(InstallerStorageUpdate update,CancellationToken t) { if(FailStorageUpdate) { FailStorageUpdate=false; throw new IOException("database failure"); } var index=Files.FindIndex(x=>x.Id==update.InstallerId); var old=Files[index]; Files[index]=old with { ScanRootId=update.ScanRootId,RelativePath=update.RelativePath,StorageState=update.StorageState,Sha256=update.Sha256??old.Sha256,OriginalScanRootId=update.OriginalScanRootId,OriginalRelativePath=update.OriginalRelativePath,StorageChangedUtc=update.ChangedUtc }; return Task.CompletedTask; }
        public Task SetInstallerPinnedAsync(long id,bool value,CancellationToken t)=>Task.CompletedTask; public Task MarkInstallerPurgedAsync(long id,CancellationToken t) { var i=Files.FindIndex(x=>x.Id==id);Files[i]=Files[i] with { Exists=false };return Task.CompletedTask; } public Task SaveArchiveOperationAsync(ArchiveOperation operation,CancellationToken t) { Operations.RemoveAll(x=>x.Id==operation.Id);Operations.Add(operation);return Task.CompletedTask; } public Task<IReadOnlyList<ArchiveOperation>> GetArchiveOperationsAsync(long? id,CancellationToken t)=>Task.FromResult<IReadOnlyList<ArchiveOperation>>(Operations);
    }
}

using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

public sealed class CatalogScanner(IInstallerFileRepository repository, IFileHashCalculator hashCalculator, CatalogScannerOptions options)
{
    public async Task<ScanResult> ScanAsync(IEnumerable<string> rootFolders, CancellationToken cancellationToken)
    {
        var files = new List<string>(); var errors = new List<ScanError>();
        foreach (var root in rootFolders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root)) { errors.Add(new(root, "The scan folder does not exist.")); continue; }
            try { files.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(path => options.SupportedExtensions.Contains(Path.GetExtension(path)))); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { errors.Add(new(root, exception.Message)); }
        }
        var processed = 0;
        await Parallel.ForEachAsync(files, new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = options.MaxDegreeOfParallelism }, async (path, token) =>
        {
            try { await ScanFileAsync(path, token); Interlocked.Increment(ref processed); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { lock (errors) errors.Add(new(path, exception.Message)); }
        });
        return new(files.Count, processed, errors);
    }
    private async Task ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path); var observedAt = DateTimeOffset.UtcNow; var current = await repository.FindByPathAsync(info.FullName, cancellationToken);
        var unchanged = current is not null && current.Size == info.Length && current.LastWriteTimeUtc == info.LastWriteTimeUtc;
        var hash = unchanged ? current!.Sha256 : await hashCalculator.ComputeSha256Async(info.FullName, cancellationToken);
        await repository.UpsertAsync(new(info.FullName, info.Name, info.Extension.ToLowerInvariant(), info.Length, info.LastWriteTimeUtc, hash, current?.FirstSeenUtc ?? observedAt, observedAt, true), cancellationToken);
    }
}
public sealed record ScanResult(int DiscoveredFiles, int ProcessedFiles, IReadOnlyList<ScanError> Errors);
public sealed record ScanError(string Path, string Message);

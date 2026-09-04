using System.Threading.Channels;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

public sealed class CatalogScanner(IScanCatalogRepository repository, IPortablePathResolver paths, IFileHashCalculator hashCalculator, CatalogScannerOptions options)
{
    private const int BatchSize = 100;

    public async Task<ScanResult> ScanAsync(ScanRoot root, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var errors = new List<ScanError>();
        var counters = new Counters();
        var resolvedRoot = paths.Resolve(root);
        if (!Directory.Exists(resolvedRoot)) return new ScanResult(0, 0, [new ScanError(resolvedRoot, "Scan root does not exist.")], false);

        var input = Channel.CreateBounded<string>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
        var output = Channel.CreateBounded<InstallerFile>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
        var reporter = new ProgressReporter(progress, counters, errors);
        var writer = WriteAsync(output.Reader, repository, cancellationToken);
        var workers = Enumerable.Range(0, options.MaxDegreeOfParallelism).Select(_ => ProcessAsync(input.Reader, output.Writer, root, reporter, errors, cancellationToken)).ToArray();
        try
        {
            await Task.Run(async () =>
            {
                foreach (var path in EnumerateFilesSafely(resolvedRoot, root.IncludeSubdirectories, cancellationToken, errors))
                {
                    if (!options.SupportedExtensions.Contains(Path.GetExtension(path))) continue;
                    await input.Writer.WriteAsync(path, cancellationToken); Interlocked.Increment(ref counters.Discovered); reporter.Report();
                }
            }, cancellationToken);
            input.Writer.TryComplete(); await Task.WhenAll(workers); output.Writer.TryComplete(); await writer;
            await repository.MarkMissingAsync(root.Id, started, cancellationToken);
            reporter.Report(); return new(counters.Discovered, counters.Processed, errors, true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            input.Writer.TryComplete(); output.Writer.TryComplete(); return new(counters.Discovered, counters.Processed, errors, false);
        }
    }

    private async Task ProcessAsync(ChannelReader<string> reader, ChannelWriter<InstallerFile> writer, ScanRoot root, ProgressReporter reporter, List<ScanError> errors, CancellationToken token)
    {
        await foreach (var path in reader.ReadAllAsync(token))
        {
            try
            {
                var info = new FileInfo(path); var relative = paths.GetRelativePath(root, info.FullName); var existing = await repository.FindInstallerAsync(root.Id, relative, token);
                var unchanged = existing is not null && existing.Size == info.Length && existing.LastWriteTimeUtc == info.LastWriteTimeUtc;
                var hash = unchanged ? existing!.Sha256 : await hashCalculator.ComputeSha256Async(info.FullName, token); var now = DateTimeOffset.UtcNow;
                await writer.WriteAsync(new(existing?.Id ?? 0, root.Id, relative, info.Name, info.Extension.ToLowerInvariant(), info.Length, info.LastWriteTimeUtc, hash, existing?.FirstSeenUtc ?? now, now, true), token);
                Interlocked.Increment(ref reporter.Counters.Processed); reporter.Report();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { lock (errors) errors.Add(new(path, exception.Message)); Interlocked.Increment(ref reporter.Counters.Errors); reporter.Report(); }
        }
    }

    private static async Task WriteAsync(ChannelReader<InstallerFile> reader, IScanCatalogRepository repository, CancellationToken token)
    {
        var batch = new List<InstallerFile>(BatchSize);
        await foreach (var item in reader.ReadAllAsync(token)) { batch.Add(item); if (batch.Count == BatchSize) { await repository.UpsertInstallersAsync(batch, token); batch.Clear(); } }
        if (batch.Count > 0) await repository.UpsertInstallersAsync(batch, token);
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, bool recurse, CancellationToken token, List<ScanError> errors)
    {
        var directories = new Stack<string>(); directories.Push(root);
        while (directories.Count > 0)
        {
            token.ThrowIfCancellationRequested(); var directory = directories.Pop();
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(directory); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { errors.Add(new(directory, exception.Message)); continue; }
            foreach (var file in files) { token.ThrowIfCancellationRequested(); yield return file; }
            if (!recurse) continue;
            IEnumerable<string> children;
            try { children = Directory.EnumerateDirectories(directory); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { errors.Add(new(directory, exception.Message)); continue; }
            foreach (var child in children) { token.ThrowIfCancellationRequested(); if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) directories.Push(child); }
        }
    }
}
public sealed record ScanResult(int DiscoveredFiles, int ProcessedFiles, IReadOnlyList<ScanError> Errors, bool Completed);
public sealed record ScanError(string Path, string Message);
public sealed record ScanProgress(int Discovered, int Processed, int Errors, string? CurrentFile);
public sealed class Counters { public int Discovered; public int Processed; public int Errors; }
public sealed class ProgressReporter(IProgress<ScanProgress>? progress, Counters counters, List<ScanError> errors)
{
    private long _last; public Counters Counters => counters;
    public void Report() { if (progress is null || Environment.TickCount64 - Interlocked.Read(ref _last) < 150) return; Interlocked.Exchange(ref _last, Environment.TickCount64); progress.Report(new(Volatile.Read(ref counters.Discovered), Volatile.Read(ref counters.Processed), errors.Count, null)); }
}

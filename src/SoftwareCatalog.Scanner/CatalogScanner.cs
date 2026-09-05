using System.Collections.Concurrent;
using System.Threading.Channels;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Scanner;

public sealed class CatalogScanner(
    IScanCatalogRepository repository,
    IPortablePathResolver paths,
    IFileHashCalculator hashCalculator,
    CatalogScannerOptions options,
    IAppLogger? logger = null,
    IFileSystemEnumerator? fileSystemEnumerator = null,
    InstallerMetadataService? metadataService = null)
{
    private const int BatchSize = 100;
    private readonly IFileSystemEnumerator _fileSystemEnumerator = fileSystemEnumerator ?? new SafeFileSystemEnumerator();
    private readonly InstallerMetadataService _metadataService = metadataService ?? new InstallerMetadataService();

    public async Task<ScanResult> ScanAsync(
        ScanRoot root,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var errors = new ConcurrentQueue<ScanError>();
        var counters = new ScanCounters();
        var resolvedRoot = paths.Resolve(root);
        logger?.Information("scan", $"Starting root '{resolvedRoot}'.");
        if (!Directory.Exists(resolvedRoot))
        {
            return new ScanResult(0, 0, [new ScanError(resolvedRoot, "Scan root does not exist.")], false);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = linkedCancellation.Token;
        var input = Channel.CreateBounded<string>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
        var output = Channel.CreateBounded<InstallerFile>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait });
        var reporter = new ProgressReporter(progress, counters, errors);
        var completeTraversal = true;
        Task? producer = null;
        Task? workersCompletion = null;
        Task? writer = null;

        try
        {
            producer = ProduceAsync(resolvedRoot, root.IncludeSubdirectories, input.Writer, errors, counters, reporter, token, () => completeTraversal = false);
            var workers = Enumerable.Range(0, options.MaxDegreeOfParallelism).Select(_ => ProcessAsync(input.Reader, output.Writer, root, errors, counters, reporter, token)).ToArray();
            writer = WriteAsync(output.Reader, token);
            workersCompletion = Task.WhenAll(workers);
            CancelOnFault(producer, linkedCancellation);
            CancelOnFault(workersCompletion, linkedCancellation);
            CancelOnFault(writer, linkedCancellation);
            await producer;
            input.Writer.TryComplete();
            await workersCompletion;
            output.Writer.TryComplete();
            await writer;

            if (completeTraversal && errors.IsEmpty)
            {
                await repository.MarkMissingAsync(root.Id, started, token);
            }

            reporter.Report(force: true);
            logger?.Information("scan", $"Completed root '{resolvedRoot}'.");
            return new ScanResult(counters.Discovered, counters.Processed, errors.ToArray(), completeTraversal && errors.IsEmpty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            linkedCancellation.Cancel();
            logger?.Information("scan", $"Cancelled root '{resolvedRoot}'.");
            return new ScanResult(counters.Discovered, counters.Processed, errors.ToArray(), false);
        }
        catch (Exception exception)
        {
            linkedCancellation.Cancel();
            errors.Enqueue(new ScanError(resolvedRoot, exception.Message));
            logger?.Error("scan", exception.Message);
            return new ScanResult(counters.Discovered, counters.Processed, errors.ToArray(), false);
        }
        finally
        {
            linkedCancellation.Cancel();
            input.Writer.TryComplete();
            output.Writer.TryComplete();
            await AwaitCompletionAsync(producer, workersCompletion, writer);
        }
    }

    private static async Task AwaitCompletionAsync(params Task?[] tasks)
    {
        foreach (var task in tasks.Where(task => task is not null))
        {
            try { await task!; }
            catch (OperationCanceledException) { }
            catch { }
        }
    }

    private static void CancelOnFault(Task task, CancellationTokenSource cancellation)
    {
        _ = task.ContinueWith(
            _ => cancellation.Cancel(),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task ProduceAsync(string root, bool recurse, ChannelWriter<string> writer, ConcurrentQueue<ScanError> errors, ScanCounters counters, ProgressReporter reporter, CancellationToken token, Action markPartial)
    {
        await Task.Run(async () =>
        {
            foreach (var file in _fileSystemEnumerator.EnumerateFiles(root, recurse, errors, token, markPartial))
            {
                if (!options.SupportedExtensions.Contains(Path.GetExtension(file))) continue;
                await writer.WriteAsync(file, token);
                Interlocked.Increment(ref counters.Discovered);
                reporter.Report();
            }
        }, token);
    }

    private async Task ProcessAsync(ChannelReader<string> reader, ChannelWriter<InstallerFile> writer, ScanRoot root, ConcurrentQueue<ScanError> errors, ScanCounters counters, ProgressReporter reporter, CancellationToken token)
    {
        await foreach (var path in reader.ReadAllAsync(token))
        {
            try
            {
                var info = new FileInfo(path);
                var relativePath = paths.GetRelativePath(root, info.FullName);
                var existing = await repository.FindInstallerAsync(root.Id, relativePath, token);
                var unchanged = existing is not null && existing.Size == info.Length && existing.LastWriteTimeUtc == info.LastWriteTimeUtc;
                var hash = unchanged ? existing!.Sha256 : await hashCalculator.ComputeSha256Async(info.FullName, token);
                var reuseMetadata = unchanged && existing!.MetadataStatus is MetadataStatus.Success or MetadataStatus.Partial;
                var metadata = reuseMetadata ? null : _metadataService.Extract(info.FullName);
                var now = DateTimeOffset.UtcNow;
                var file = new InstallerFile(existing?.Id ?? 0, root.Id, relativePath, info.Name, info.Extension.ToLowerInvariant(), info.Length, info.LastWriteTimeUtc, hash, existing?.FirstSeenUtc ?? now, now, true);
                file = reuseMetadata ? file with { InstallerKind = existing!.InstallerKind, ProductName = existing.ProductName, ProductVersion = existing.ProductVersion, Publisher = existing.Publisher, FileVersion = existing.FileVersion, FileDescription = existing.FileDescription, Architecture = existing.Architecture, MetadataSource = existing.MetadataSource, MetadataStatus = existing.MetadataStatus, MetadataError = existing.MetadataError, NormalizedVersion = existing.NormalizedVersion, ProductCode = existing.ProductCode, UpgradeCode = existing.UpgradeCode, PackageList = existing.PackageList } : file with { InstallerKind = metadata!.Kind, ProductName = metadata.ProductName, ProductVersion = metadata.ProductVersion, Publisher = metadata.Publisher, FileVersion = metadata.FileVersion, FileDescription = metadata.FileDescription, Architecture = metadata.Architecture, MetadataSource = metadata.Source, MetadataStatus = metadata.Status, MetadataError = metadata.Error, NormalizedVersion = VersionNormalizer.Normalize(metadata.ProductVersion), ProductCode = metadata.ProductCode, UpgradeCode = metadata.UpgradeCode, PackageList = metadata.PackageList };
                if (!reuseMetadata && file.MetadataStatus == MetadataStatus.Failed) logger?.Error("metadata", $"metadata extraction failed path='{info.FullName}' kind={file.InstallerKind} extractor={file.MetadataSource} error={file.MetadataError}");
                await writer.WriteAsync(file, token);
                Interlocked.Increment(ref counters.Processed);
                reporter.Report();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Enqueue(new ScanError(path, exception.Message));
                Interlocked.Increment(ref counters.Errors);
                reporter.Report();
            }
        }
    }

    private async Task WriteAsync(ChannelReader<InstallerFile> reader, CancellationToken token)
    {
        var batch = new List<InstallerFile>(BatchSize);
        await foreach (var file in reader.ReadAllAsync(token))
        {
            batch.Add(file);
            if (batch.Count < BatchSize) continue;
            await repository.UpsertInstallersAsync(batch, token);
            batch.Clear();
        }

        if (batch.Count > 0) await repository.UpsertInstallersAsync(batch, token);
    }

}

public sealed class SafeFileSystemEnumerator : IFileSystemEnumerator
{
    public IEnumerable<string> EnumerateFiles(string root, bool recurse, ConcurrentQueue<ScanError> errors, CancellationToken token, Action markPartial)
    {
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var directory = directories.Pop();
            var files = Array.Empty<string>();
            try { files = Directory.EnumerateFiles(directory).ToArray(); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { errors.Enqueue(new ScanError(directory, exception.Message)); markPartial(); }
            foreach (var file in files) { token.ThrowIfCancellationRequested(); yield return file; }
            if (!recurse) continue;
            IEnumerator<string>? children = null;
            try { children = Directory.EnumerateDirectories(directory).GetEnumerator(); while (children.MoveNext()) { token.ThrowIfCancellationRequested(); var child = children.Current; try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) directories.Push(child); } catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { errors.Enqueue(new ScanError(child, exception.Message)); markPartial(); } } }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { errors.Enqueue(new ScanError(directory, exception.Message)); markPartial(); }
            finally { children?.Dispose(); }
        }
    }
}

public sealed record ScanResult(int DiscoveredFiles, int ProcessedFiles, IReadOnlyList<ScanError> Errors, bool Completed);
public sealed record ScanError(string Path, string Message);
public sealed record ScanProgress(int Discovered, int Processed, int Errors, string? CurrentFile);
public sealed class ScanCounters { public int Discovered; public int Processed; public int Errors; }
public sealed class ProgressReporter(IProgress<ScanProgress>? progress, ScanCounters counters, ConcurrentQueue<ScanError> errors)
{
    private long _last;
    public void Report(bool force = false)
    {
        if (progress is null || (!force && Environment.TickCount64 - Interlocked.Read(ref _last) < 150)) return;
        Interlocked.Exchange(ref _last, Environment.TickCount64);
        progress.Report(new ScanProgress(Volatile.Read(ref counters.Discovered), Volatile.Read(ref counters.Processed), errors.Count, null));
    }
}

using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed record UpdateBatchResult(int Checked, int Updates, int Errors, int Ambiguous);
public sealed class UpdateBatchService(IUpdateChecker discovery, IAppLogger? logger = null)
{
    public async Task<UpdateBatchResult> CheckAsync(IEnumerable<SoftwareProduct> products, bool force, int cacheHours, int maxParallelism, CancellationToken cancellationToken)
    {
        var list = products.ToArray(); logger?.Information("update-batch", $"update batch started count={list.Length}"); var results = new List<UpdateCheckResult>(); using var gate = new SemaphoreSlim(Math.Max(1, maxParallelism));
        await Task.WhenAll(list.Select(async product => { await gate.WaitAsync(cancellationToken); try { var result = await discovery.CheckAsync(product, force, cacheHours, cancellationToken); lock (results) results.Add(result); } finally { gate.Release(); } }));
        var summary = new UpdateBatchResult(results.Count, results.Count(result => result.Status == UpdateStatus.UpdateAvailable), results.Count(result => result.Status == UpdateStatus.Error), results.Count(result => result.Status == UpdateStatus.Ambiguous)); logger?.Information("update-batch", $"update batch completed checked={summary.Checked} updates={summary.Updates} errors={summary.Errors} ambiguous={summary.Ambiguous}"); return summary;
    }
}

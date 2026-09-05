using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

/// <summary>Bounds concurrent download workflows; callers may queue more work safely.</summary>
public sealed class DownloadCoordinator
{
    private readonly SemaphoreSlim _gate;
    public DownloadCoordinator(int maxParallelism) => _gate = new(Math.Max(1, maxParallelism), Math.Max(1, maxParallelism));
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await operation(cancellationToken); }
        finally { _gate.Release(); }
    }
}

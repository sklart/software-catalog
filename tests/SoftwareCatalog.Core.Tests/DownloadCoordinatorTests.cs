using SoftwareCatalog.Core;

namespace SoftwareCatalog.Core.Tests;

public sealed class DownloadCoordinatorTests
{
    [Fact]
    public async Task BoundsConcurrentOperations()
    {
        var coordinator = new DownloadCoordinator(2); var active = 0; var peak = 0; var release = new TaskCompletionSource();
        async Task<int> Operation(CancellationToken token) => await coordinator.RunAsync(async _ => { var now = Interlocked.Increment(ref active); while (true) { var seen = Volatile.Read(ref peak); if (now <= seen || Interlocked.CompareExchange(ref peak, now, seen) == seen) break; } await release.Task.WaitAsync(token); Interlocked.Decrement(ref active); return now; }, token);
        var first = Operation(CancellationToken.None); var second = Operation(CancellationToken.None); var third = Operation(CancellationToken.None);
        await Task.Delay(50); Assert.Equal(2, Volatile.Read(ref active)); Assert.Equal(2, Volatile.Read(ref peak)); release.SetResult(); await Task.WhenAll(first, second, third);
    }
}

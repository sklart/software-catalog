using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class UpdateBatchServiceTests
{
    [Fact]
    public async Task RespectsConcurrencyLimitAndProcessesEveryProduct()
    {
        var checker = new DelayedChecker(); var now = DateTimeOffset.UtcNow;
        var products = Enumerable.Range(0, 6).Select(index => new SoftwareProduct(Guid.NewGuid(), $"Tool {index}", null, $"tool{index}", now, now, "1.0", "1.0")).ToArray();
        var result = await new UpdateBatchService(checker).CheckAsync(products, true, 12, 2, CancellationToken.None);
        Assert.Equal(6, result.Checked); Assert.Equal(6, checker.Calls); Assert.InRange(checker.Peak, 1, 2);
    }
    [Fact]
    public async Task NormalizesNonPositiveConcurrencyToOne()
    {
        var checker = new DelayedChecker(); var now = DateTimeOffset.UtcNow; var products = Enumerable.Range(0, 3).Select(index => new SoftwareProduct(Guid.NewGuid(), $"Tool {index}", null, $"tool{index}", now, now)).ToArray();
        await new UpdateBatchService(checker).CheckAsync(products, true, 12, 0, CancellationToken.None); Assert.Equal(1, checker.Peak);
    }
    private sealed class DelayedChecker : IUpdateChecker
    {
        private int _current; public int Calls; public int Peak;
        public async Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken token) { Interlocked.Increment(ref Calls); var current = Interlocked.Increment(ref _current); while (true) { var old = Peak; if (old >= current || Interlocked.CompareExchange(ref Peak, current, old) == old) break; } await Task.Delay(25, token); Interlocked.Decrement(ref _current); return new(UpdateStatus.UpdateAvailable); }
    }
}

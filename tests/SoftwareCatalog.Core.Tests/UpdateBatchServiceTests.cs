using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
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
    [Fact]
    public async Task PropagatesCancellationAndDoesNotStartQueuedChecks()
    {
        var checker = new BlockingChecker(); var now = DateTimeOffset.UtcNow; var products = Enumerable.Range(0, 5).Select(i => new SoftwareProduct(Guid.NewGuid(), $"P{i}", null, $"p{i}", now, now)).ToArray(); using var cancel = new CancellationTokenSource();
        var task = new UpdateBatchService(checker).CheckAsync(products, true, 12, 2, cancel.Token); await checker.Started.Task.WaitAsync(TimeSpan.FromSeconds(2)); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); Assert.InRange(checker.Calls, 1, 2); Assert.True(checker.Cancelled);
    }
    [Fact]
    public async Task WritesMeaningfulBatchSummary()
    {
        var logger = new Logger(); var now = DateTimeOffset.UtcNow; var checker = new ResultsChecker([UpdateStatus.UpdateAvailable, UpdateStatus.UpToDate, UpdateStatus.Error, UpdateStatus.Ambiguous]); var products = Enumerable.Range(0, 4).Select(i => new SoftwareProduct(Guid.NewGuid(), $"P{i}", null, $"p{i}", now, now));
        await new UpdateBatchService(checker, logger).CheckAsync(products, true, 12, 2, CancellationToken.None); var text = string.Join(" ", logger.Messages); Assert.Contains("update batch started", text); Assert.Contains("update batch completed", text); Assert.Contains("checked=4", text); Assert.Contains("updates=1", text); Assert.Contains("errors=1", text); Assert.Contains("ambiguous=1", text);
    }
    private sealed class DelayedChecker : IUpdateChecker
    {
        private int _current; public int Calls; public int Peak;
        public async Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken token) { Interlocked.Increment(ref Calls); var current = Interlocked.Increment(ref _current); while (true) { var old = Peak; if (old >= current || Interlocked.CompareExchange(ref Peak, current, old) == old) break; } await Task.Delay(25, token); Interlocked.Decrement(ref _current); return new(UpdateStatus.UpdateAvailable); }
    }
    private sealed class BlockingChecker : IUpdateChecker
    {
        public int Calls; public bool Cancelled; public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken token) { Interlocked.Increment(ref Calls); Started.TrySetResult(); try { await Task.Delay(Timeout.InfiniteTimeSpan, token); return new(UpdateStatus.UpToDate); } catch (OperationCanceledException) { Cancelled = true; throw; } }
    }
    private sealed class ResultsChecker(IEnumerable<UpdateStatus> values) : IUpdateChecker { private readonly Queue<UpdateStatus> _values = new(values); public Task<UpdateCheckResult> CheckAsync(SoftwareProduct p,bool f,int h,CancellationToken t)=>Task.FromResult(new UpdateCheckResult(_values.Dequeue())); }
    private sealed class Logger : IAppLogger { public List<string> Messages { get; }=[]; public void Information(string operation,string message)=>Messages.Add(message); public void Error(string operation,string message)=>Messages.Add(message); }
}

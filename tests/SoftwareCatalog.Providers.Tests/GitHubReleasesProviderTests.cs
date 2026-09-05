using System.Net;
using System.Net.Http;
using System.Text;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Providers;

namespace SoftwareCatalog.Providers.Tests;

public sealed class GitHubReleasesProviderTests
{
    [Fact]
    public async Task ReadsCompleteReleasePayload()
    {
        var json = """{"tag_name":"v2.0","name":"Version 2","published_at":"2026-01-02T03:04:05Z","html_url":"https://github.com/o/r/releases/tag/v2.0"}"""; var result = await Provider(HttpStatusCode.OK, json).CheckLatestAsync(Product(), Source(), CancellationToken.None);
        Assert.Equal(UpdateStatus.Unknown, result.Status); Assert.Equal("v2.0", result.LatestVersion); Assert.Equal("2.0", result.LatestNormalizedVersion); Assert.Equal("Version 2", result.ReleaseName); Assert.Equal("GitHub", result.Source); Assert.Equal("owner/repo", result.ExternalProductId); Assert.Equal(new Uri("https://github.com/o/r/releases/tag/v2.0"), result.DownloadPageUrl);
    }
    [Theory]
    [InlineData(HttpStatusCode.NotFound, UpdateStatus.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, UpdateStatus.Error)]
    [InlineData(HttpStatusCode.TooManyRequests, UpdateStatus.Error)]
    [InlineData(HttpStatusCode.InternalServerError, UpdateStatus.Error)]
    public async Task MapsHttpFailures(HttpStatusCode code, UpdateStatus status) => Assert.Equal(status, (await Provider(code, "{}").CheckLatestAsync(Product(), Source(), CancellationToken.None)).Status);
    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    public async Task ReportsMalformedResponses(string body) => Assert.Equal(UpdateStatus.Error, (await Provider(HttpStatusCode.OK, body).CheckLatestAsync(Product(), Source(), CancellationToken.None)).Status);
    [Fact] public async Task RejectsInvalidRepositoryAndPropagatesCancellation() { Assert.Equal(UpdateStatus.NotFound, (await Provider(HttpStatusCode.OK, "{}").CheckLatestAsync(Product(), Source("invalid"), CancellationToken.None)).Status); using var cts = new CancellationTokenSource(); cts.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Provider(HttpStatusCode.OK, "{}").CheckLatestAsync(Product(), Source(), cts.Token)); }
    [Fact] public async Task MapsNetworkTimeoutToErrorButPreservesUserCancellation() { var timeout = new GitHubReleasesProvider(new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout"))) { BaseAddress = new Uri("https://api.github.com/") }, new ProductNormalizer()); var result = await timeout.CheckLatestAsync(Product(), Source(), CancellationToken.None); Assert.Equal(UpdateStatus.Error, result.Status); Assert.Contains("timed out", result.Error!, StringComparison.OrdinalIgnoreCase); using var cts = new CancellationTokenSource(); cts.Cancel(); var cancelled = new GitHubReleasesProvider(new HttpClient(new ThrowingHandler(new TaskCanceledException())) { BaseAddress = new Uri("https://api.github.com/") }, new ProductNormalizer()); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.CheckLatestAsync(Product(), Source(), cts.Token)); }
    private static GitHubReleasesProvider Provider(HttpStatusCode status, string body) => new(new HttpClient(new Handler(status, body)) { BaseAddress = new Uri("https://api.github.com/") }, new ProductNormalizer());
    private static SoftwareProduct Product() { var now = DateTimeOffset.UtcNow; return new(Guid.NewGuid(), "Tool", null, "tool", now, now); }
    private static ProductUpdateSource Source(string id = "owner/repo") => new(Guid.NewGuid(), Guid.NewGuid(), "GitHub", id, true, true);
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }); } }
    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.FromException<HttpResponseMessage>(exception); } }
}

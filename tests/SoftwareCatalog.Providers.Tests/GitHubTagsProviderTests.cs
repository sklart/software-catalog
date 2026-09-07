using System.Net;
using System.Text;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Providers;

namespace SoftwareCatalog.Providers.Tests;
public sealed class GitHubTagsProviderTests
{
    [Fact] public async Task ReadsAndNormalizesLatestTag(){var result=await Provider(HttpStatusCode.OK,"[{\"name\":\"v2.0\"}]").CheckLatestAsync(Product(),Source(),CancellationToken.None);Assert.Equal(UpdateStatus.Unknown,result.Status);Assert.Equal("v2.0",result.LatestVersion);Assert.Equal("2.0",result.LatestNormalizedVersion);}
    [Theory][InlineData(HttpStatusCode.NotFound)][InlineData(HttpStatusCode.OK)] public async Task MapsNotFoundAndEmptyTags(HttpStatusCode status){var result=await Provider(status,"[]").CheckLatestAsync(Product(),Source(),CancellationToken.None);Assert.Equal(UpdateStatus.NotFound,result.Status);}
    [Fact] public async Task MapsServerAndNetworkFailures(){Assert.Equal(UpdateStatus.Error,(await Provider(HttpStatusCode.InternalServerError,"{}").CheckLatestAsync(Product(),Source(),CancellationToken.None)).Status);var network=new GitHubTagsProvider(new HttpClient(new ThrowingHandler(new HttpRequestException("offline"))){BaseAddress=new Uri("https://api.github.com/")},new ProductNormalizer());Assert.Equal(UpdateStatus.Error,(await network.CheckLatestAsync(Product(),Source(),CancellationToken.None)).Status);}
    [Fact] public async Task RejectsMalformedAndPropagatesCancellation(){Assert.Equal(UpdateStatus.Error,(await Provider(HttpStatusCode.OK,"not-json").CheckLatestAsync(Product(),Source(),CancellationToken.None)).Status);using var cts=new CancellationTokenSource();cts.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Provider(HttpStatusCode.OK,"[]").CheckLatestAsync(Product(),Source(),cts.Token));}
    private static GitHubTagsProvider Provider(HttpStatusCode status,string body)=>new(new HttpClient(new Handler(status,body)){BaseAddress=new Uri("https://api.github.com/")},new ProductNormalizer());
    private static SoftwareProduct Product(){var now=DateTimeOffset.UtcNow;return new(Guid.NewGuid(),"Tool",null,"tool",now,now);}
    private static ProductUpdateSource Source()=>new(Guid.NewGuid(),Guid.NewGuid(),"GitHubTags","owner/repo",true,true);
    private sealed class Handler(HttpStatusCode status,string body):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")});}}
    private sealed class ThrowingHandler(Exception exception):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromException<HttpResponseMessage>(exception);}
}

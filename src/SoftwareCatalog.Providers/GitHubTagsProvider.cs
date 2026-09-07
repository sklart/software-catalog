using System.Net;
using System.Net.Http.Json;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
namespace SoftwareCatalog.Providers;
public sealed class GitHubTagsProvider(HttpClient client, ProductNormalizer normalizer) : IUpdateProvider
{
    public string Id => "GitHubTags";
    public bool CanHandle(SoftwareProduct product, ProductUpdateSource? source) => source?.Enabled == true && source.ProviderType.Equals(Id, StringComparison.OrdinalIgnoreCase);
    public async Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token)
    {
        if (source is null || source.ExternalId.Split('/').Length != 2) return new(UpdateStatus.NotFound, Source: Id, Error: "GitHub repository is not configured");
        try { using var response=await client.GetAsync($"repos/{source.ExternalId}/tags?per_page=1",token); if(response.StatusCode==HttpStatusCode.NotFound)return new(UpdateStatus.NotFound,Source:Id,ExternalProductId:source.ExternalId); if(!response.IsSuccessStatusCode)return new(UpdateStatus.Error,Source:Id,ExternalProductId:source.ExternalId,Error:$"GitHub returned {(int)response.StatusCode}"); var tags=await response.Content.ReadFromJsonAsync<Tag[]>(cancellationToken:token); var tag=tags?.FirstOrDefault()?.name; return string.IsNullOrWhiteSpace(tag) ? new(UpdateStatus.NotFound,Source:Id,ExternalProductId:source.ExternalId) : new(UpdateStatus.Unknown,tag,normalizer.NormalizeVersion(tag),tag,null,new Uri($"https://github.com/{source.ExternalId}/tags"),Id,source.ExternalId); } catch(OperationCanceledException) when(!token.IsCancellationRequested){return new(UpdateStatus.Error,Source:Id,ExternalProductId:source.ExternalId,Error:"GitHub request timed out");} catch(HttpRequestException ex){return new(UpdateStatus.Error,Source:Id,ExternalProductId:source.ExternalId,Error:ex.Message);}
    }
    private sealed record Tag(string? name);
}

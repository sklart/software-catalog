using System.Net;
using System.Net.Http.Json;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Providers;

public sealed class GitHubReleasesProvider(HttpClient client, ProductNormalizer normalizer) : IUpdateProvider
{
    public string Id => "GitHub";
    public bool CanHandle(SoftwareProduct product, ProductUpdateSource? source) => source?.Enabled == true && source.ProviderType.Equals(Id, StringComparison.OrdinalIgnoreCase);
    public async Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token)
    {
        if (source is null || !IsRepository(source.ExternalId)) return new(UpdateStatus.NotFound, Error: "GitHub repository is not configured");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{source.ExternalId}/releases/latest");
        using var response = await client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateStatus.NotFound, Source: Id, ExternalProductId: source.ExternalId);
        if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "GitHub rate limit reached");
        if (!response.IsSuccessStatusCode) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: $"GitHub returned {(int)response.StatusCode}");
        var release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken: token);
        if (release?.tag_name is not { Length: > 0 } tag) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "Malformed GitHub release response");
        return new(UpdateStatus.Unknown, tag, normalizer.NormalizeVersion(tag), release.name, release.published_at, Uri.TryCreate(release.html_url, UriKind.Absolute, out var uri) ? uri : null, Id, source.ExternalId);
    }
    private static bool IsRepository(string value) => value.Split('/').Length == 2 && !value.Contains(' ');
    private sealed record Release(string? tag_name, string? name, DateTimeOffset? published_at, string? html_url);
}

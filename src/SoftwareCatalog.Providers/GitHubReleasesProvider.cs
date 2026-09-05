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
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "GitHub request timed out"); }
        catch (HttpRequestException ex) { return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: ex.Message); }
        using (response)
        {
        if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateStatus.NotFound, Source: Id, ExternalProductId: source.ExternalId);
        if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "GitHub rate limit reached");
        if (!response.IsSuccessStatusCode) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: $"GitHub returned {(int)response.StatusCode}");
        Release? release;
        try { release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken: token); }
        catch (System.Text.Json.JsonException) { return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "Malformed GitHub release response"); }
        if (release?.tag_name is not { Length: > 0 } tag) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "Malformed GitHub release response");
        return new(UpdateStatus.Unknown, tag, normalizer.NormalizeVersion(tag), release.name, release.published_at, Uri.TryCreate(release.html_url, UriKind.Absolute, out var uri) ? uri : null, Id, source.ExternalId);
        }
    }
    private static bool IsRepository(string value) => value.Split('/').Length == 2 && !value.Contains(' ');
    private sealed record Release(string? tag_name, string? name, DateTimeOffset? published_at, string? html_url);
}

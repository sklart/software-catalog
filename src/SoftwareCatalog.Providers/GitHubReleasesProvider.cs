using System.Net;
using System.Net.Http.Json;
using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Providers;

public sealed class GitHubReleasesProvider(HttpClient client, ProductNormalizer normalizer) : IUpdateProvider, IUpdateDownloadProvider, IUpdateCandidateProvider
{
    public string Id => "GitHub";
    public bool CanHandle(SoftwareProduct product, ProductUpdateSource? source) => source?.Enabled == true && source.ProviderType.Equals(Id, StringComparison.OrdinalIgnoreCase);
    public async Task<UpdateCheckResult> CheckLatestAsync(SoftwareProduct product, ProductUpdateSource? source, CancellationToken token)
    {
        if (source is null || !IsRepository(source.ExternalId)) return new(UpdateStatus.NotFound, Error: "GitHub repository is not configured");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"repos/{source.ExternalId}/releases/latest");
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "GitHub request timed out", ErrorKind: ProviderErrorKind.Timeout); }
        catch (HttpRequestException ex) { return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: ex.Message, ErrorKind: ProviderErrorKind.NetworkError); }
        using (response)
        {
        if (response.StatusCode == HttpStatusCode.NotFound) return new(UpdateStatus.NotFound, Source: Id, ExternalProductId: source.ExternalId, ErrorKind: ProviderErrorKind.NotFound);
        if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "GitHub rate limit reached", ErrorKind: ProviderErrorKind.RateLimited);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "GitHub authentication required", ErrorKind: ProviderErrorKind.AuthenticationRequired);
        if (!response.IsSuccessStatusCode) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: $"GitHub returned {(int)response.StatusCode}", ErrorKind: ProviderErrorKind.NetworkError);
        Release? release;
        try { release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken: token); }
        catch (System.Text.Json.JsonException) { return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "Malformed GitHub release response", ErrorKind: ProviderErrorKind.InvalidResponse); }
        if (release?.tag_name is not { Length: > 0 } tag) return new(UpdateStatus.Error, Source: Id, ExternalProductId: source.ExternalId, Error: "Malformed GitHub release response", ErrorKind: ProviderErrorKind.InvalidResponse);
        return new(UpdateStatus.Unknown, tag, normalizer.NormalizeVersion(tag), release.name, release.published_at, Uri.TryCreate(release.html_url, UriKind.Absolute, out var uri) ? uri : null, Id, source.ExternalId);
        }
    }
    private static bool IsRepository(string value) => value.Split('/').Length == 2 && !value.Contains(' ');
    public async Task<IReadOnlyList<UpdateCandidate>> SearchCandidatesAsync(SoftwareProduct product, IReadOnlyList<ProductAlias> aliases, CancellationToken token)
    {
        var queries = new[] { product.CanonicalName }.Concat(aliases.Select(x => x.Alias)).Distinct(StringComparer.OrdinalIgnoreCase);
        try { var responses=await Task.WhenAll(queries.Select(async query => { using var response=await client.GetAsync($"search/repositories?q={Uri.EscapeDataString(query)}&per_page=10",token); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SearchResult>(cancellationToken:token) : null; })); return responses.Where(x=>x is not null).SelectMany(x=>x!.items??[]).GroupBy(x=>x.full_name,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).Select(x => { var name = x.name ?? ""; var match = normalizer.Normalize(name) == product.NormalizedName || aliases.Any(a => normalizer.Normalize(name) == a.NormalizedAlias); var owner = x.owner?.login; return new UpdateCandidate(Id, x.full_name ?? "", x.full_name ?? name, owner, null, match ? MappingConfidence.High : MappingConfidence.Low, match ? "имя репозитория или alias" : "результат GitHub поиска"); }).Where(x => IsRepository(x.ExternalId)).OrderBy(x => x.Confidence).ToArray(); } catch (HttpRequestException) { return []; }
    }
    public async Task<DownloadCandidateResolution> ResolveAsync(SoftwareProduct product, ProductUpdateSource source, UpdateCheckResult? update, CancellationToken token)
    {
        if (!IsRepository(source.ExternalId)) return new(DownloadCandidateStatus.NotFound, [], "GitHub repository is not configured");
        HttpResponseMessage response;
        try { response = await client.GetAsync($"repos/{source.ExternalId}/releases/latest", token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(DownloadCandidateStatus.Error, [], "GitHub request timed out"); }
        catch (HttpRequestException ex) { return new(DownloadCandidateStatus.Error, [], ex.Message); }
        using (response)
        {
        if (response.StatusCode == HttpStatusCode.NotFound) return new(DownloadCandidateStatus.NotFound, []);
        if (!response.IsSuccessStatusCode) return new(DownloadCandidateStatus.Error, [], $"GitHub returned {(int)response.StatusCode}");
        Release? release;
        try { release = await response.Content.ReadFromJsonAsync<Release>(cancellationToken: token); } catch (OperationCanceledException) when (!token.IsCancellationRequested) { return new(DownloadCandidateStatus.Error, [], "GitHub request timed out"); } catch (System.Text.Json.JsonException) { return new(DownloadCandidateStatus.Error, [], "Malformed GitHub release response"); }
        if (release is null) return new(DownloadCandidateStatus.Error, [], "Malformed GitHub release response");
        var candidates = (release.assets ?? []).Where(IsInstaller).Select(a => Uri.TryCreate(a.browser_download_url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? new DownloadCandidate(Id, source.ExternalId, release.tag_name, normalizer.NormalizeVersion(release.tag_name), a.name, uri, a.content_type, a.size, Architecture(a.name), Path.GetExtension(a.name ?? string.Empty).TrimStart('.'), null, null, release.name, release.published_at) : null).Where(x => x is not null).Cast<DownloadCandidate>().ToArray();
        if (candidates.Length == 0) return new(DownloadCandidateStatus.NotFound, []);
        return candidates.Length == 1 ? new(DownloadCandidateStatus.Available, candidates) : new(DownloadCandidateStatus.Ambiguous, candidates, "Несколько подходящих installer-файлов.");
        }
    }
    private static bool IsInstaller(Asset asset) { var name = asset.name ?? ""; var ext = Path.GetExtension(name); return new[] { ".exe", ".msi", ".msix", ".msixbundle", ".zip", ".7z" }.Contains(ext, StringComparer.OrdinalIgnoreCase) && !new[] { "source", "checksum", "sha256", "signature", "symbols", "debug", "portable" }.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase)); }
    private static string? Architecture(string? name) => name?.Contains("arm64", StringComparison.OrdinalIgnoreCase) == true ? "arm64" : name?.Contains("x64", StringComparison.OrdinalIgnoreCase) == true || name?.Contains("amd64", StringComparison.OrdinalIgnoreCase) == true ? "x64" : name?.Contains("x86", StringComparison.OrdinalIgnoreCase) == true ? "x86" : null;
    private sealed record Release(string? tag_name, string? name, DateTimeOffset? published_at, string? html_url, Asset[]? assets = null);
    private sealed record Asset(string? name, string? browser_download_url, long? size, string? content_type);
    private sealed record SearchResult(SearchRepository[]? items);
    private sealed record SearchRepository(string? full_name, string? name, SearchOwner? owner);
    private sealed record SearchOwner(string? login);
}

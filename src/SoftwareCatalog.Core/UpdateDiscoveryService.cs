using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public interface IUpdateChecker { Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken cancellationToken); }

public sealed class UpdateDiscoveryService(IProductCatalogRepository repository, IEnumerable<IUpdateProvider> providers, VersionComparer comparer, IAppLogger? logger = null) : IUpdateChecker
{
    private static readonly string[] Priority = ["WinGet", "GitHub", "GitHubTags"];
    public async Task<UpdateCheckResult> CheckAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken cancellationToken)
    {
        if (!force && product.LastCheckedUtc is { } checkedUtc && checkedUtc > DateTimeOffset.UtcNow.AddHours(-cacheHours)) return new(product.UpdateStatus, product.LatestVersion, product.LatestNormalizedVersion, Source: product.UpdateProvider, ExternalProductId: product.ExternalProductId, Error: product.UpdateError, CheckedUtc: checkedUtc);
        var sources = await repository.GetUpdateSourcesAsync(product.Id, cancellationToken);
        var selected = sources.Where(s => s.Enabled).OrderByDescending(s => s.IsExplicit).ThenBy(s => Array.FindIndex(Priority, p => p.Equals(s.ProviderType, StringComparison.OrdinalIgnoreCase)) is var rank && rank >= 0 ? rank : int.MaxValue).ThenBy(s => s.ProviderType, StringComparer.Ordinal).FirstOrDefault();
        var provider = selected is null ? providers.OrderBy(p => Array.FindIndex(Priority, x => x.Equals(p.Id, StringComparison.OrdinalIgnoreCase)) is var rank && rank >= 0 ? rank : int.MaxValue).FirstOrDefault(p => p.CanHandle(product, null)) : providers.FirstOrDefault(p => p.CanHandle(product, selected));
        if (provider is null) return await Persist(product.Id, new(UpdateStatus.NotFound, Error: "No update source configured", CheckedUtc: DateTimeOffset.UtcNow), cancellationToken);
        try
        {
            var result = await provider.CheckLatestAsync(product, selected, cancellationToken);
            if (selected is null && result.Status == UpdateStatus.Unknown && !string.IsNullOrWhiteSpace(result.ExternalProductId)) await repository.SetUpdateSourceAsync(new ProductUpdateSource(Guid.NewGuid(), product.Id, provider.Id, result.ExternalProductId, true, false, MappingSource.ExactMatch, MappingConfidence.Exact), cancellationToken);
            if (result.Status == UpdateStatus.Unknown && result.LatestNormalizedVersion is not null)
            {
                result = result with { Status = comparer.Compare(product.LatestNormalizedVersion ?? product.LatestLocalVersion, result.LatestNormalizedVersion) switch { VersionComparisonResult.Older => UpdateStatus.UpdateAvailable, VersionComparisonResult.Equal => UpdateStatus.UpToDate, VersionComparisonResult.Newer => UpdateStatus.LocalNewer, _ => UpdateStatus.Unknown } };
            }
            return await Persist(product.Id, result with { CheckedUtc = DateTimeOffset.UtcNow, Source = provider.Id }, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { logger?.Error("update", $"Provider {provider.Id} failed for product {product.Id}: {ex.Message}"); return await Persist(product.Id, new(UpdateStatus.Error, Source: provider.Id, Error: ex.Message, CheckedUtc: DateTimeOffset.UtcNow), cancellationToken); }
    }
    private async Task<UpdateCheckResult> Persist(Guid id, UpdateCheckResult result, CancellationToken token) { await repository.SaveUpdateCheckAsync(id, result, token); return result; }
}

public sealed class UpdateCandidateDiscoveryService(IProductCatalogRepository repository, IEnumerable<IUpdateProvider> providers)
{
    public async Task<IReadOnlyList<UpdateCandidate>> SearchAsync(SoftwareProduct product, bool force, int cacheHours, CancellationToken token)
    {
        var cached = await repository.SearchUpdateCandidatesAsync(product.Id, force, cacheHours, token);
        if (cached.Count > 0) return cached;
        var aliases = await repository.GetProductAliasesAsync(product.Id, token);
        var work = providers.OfType<IUpdateCandidateProvider>().Select(async provider => { try { return await provider.SearchCandidatesAsync(product, aliases, token); } catch (OperationCanceledException) { throw; } catch { return (IReadOnlyList<UpdateCandidate>)[]; } });
        var candidates = (await Task.WhenAll(work)).SelectMany(x => x).OrderBy(x => x.Confidence).ThenBy(x => x.ProviderType, StringComparer.Ordinal).ThenBy(x => x.ExternalId, StringComparer.Ordinal).ToArray();
        await repository.SaveUpdateCandidatesAsync(product.Id, candidates, DateTimeOffset.UtcNow, token);
        return candidates;
    }
}

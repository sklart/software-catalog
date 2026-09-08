using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed class InstalledSoftwareMatchingService(ProductNormalizer normalizer)
{
    public async Task MatchAsync(IInstalledSoftwareRepository inventory, IProductCatalogRepository catalog, CancellationToken token)
    {
        var products = await catalog.GetProductsAsync(token);
        var aliases = new Dictionary<Guid, HashSet<string>>();
        foreach (var product in products) aliases[product.Id] = (await catalog.GetProductAliasesAsync(product.Id, token)).Select(x => x.NormalizedAlias).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in await inventory.GetInstalledSoftwareAsync(token))
        {
            if (!item.Exists || item.MatchSource is InstalledSoftwareMatchSource.Manual or InstalledSoftwareMatchSource.Explicit) continue;
            var candidates = products.Where(p => !string.IsNullOrWhiteSpace(item.ExternalId) && string.Equals(p.ExternalProductId, item.ExternalId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var source = InstalledSoftwareMatchSource.ExternalIdentity; var confidence = InstalledSoftwareMatchConfidence.Exact;
            if (candidates.Length == 0) { candidates = products.Where(p => p.NormalizedName == item.NormalizedName && !string.IsNullOrWhiteSpace(p.Publisher) && normalizer.Normalize(p.Publisher) == normalizer.Normalize(item.Publisher)).ToArray(); source = InstalledSoftwareMatchSource.NameAndPublisher; confidence = InstalledSoftwareMatchConfidence.High; }
            if (candidates.Length == 0) { candidates = products.Where(p => aliases.TryGetValue(p.Id, out var values) && values.Contains(item.NormalizedName)).ToArray(); source = InstalledSoftwareMatchSource.Alias; confidence = InstalledSoftwareMatchConfidence.Medium; }
            if (candidates.Length == 0) { candidates = products.Where(p => p.NormalizedName == item.NormalizedName).ToArray(); source = InstalledSoftwareMatchSource.NormalizedName; confidence = InstalledSoftwareMatchConfidence.Low; }
            if (candidates.Length == 1 && confidence >= InstalledSoftwareMatchConfidence.Medium) await inventory.SetInstalledSoftwareBindingAsync(item.Id, candidates[0].Id, source, confidence, token);
            else await inventory.SetInstalledSoftwareBindingAsync(item.Id, null, InstalledSoftwareMatchSource.ManualReview, candidates.Length > 1 ? InstalledSoftwareMatchConfidence.Ambiguous : InstalledSoftwareMatchConfidence.None, token);
        }
    }
}

public sealed class InstalledSoftwareInventoryService(IEnumerable<IInstalledSoftwareSource> sources, IInstalledSoftwareRepository repository, IProductCatalogRepository catalog, InstalledSoftwareMatchingService matching, VersionComparer comparer)
{
    public async Task RefreshAsync(CancellationToken token)
    {
        var started = DateTimeOffset.UtcNow;
        var raw = (await Task.WhenAll(sources.Select(source => source.ReadAsync(token)))).SelectMany(x => x);
        var distinct = raw.GroupBy(x => string.Join("|", x.NormalizedName, x.Publisher?.Trim().ToUpperInvariant(), x.NormalizedVersion, x.Architecture, x.InstallLocation?.Trim().ToUpperInvariant())).Select(g => g.OrderByDescending(x => x.Source == InstalledSoftwareSource.Msix).First()).ToList();
        await repository.UpsertInstalledSoftwareAsync(distinct, started, token);
        await matching.MatchAsync(repository, catalog, token);
    }
    public InstalledUpdateStatus GetUpdateStatus(InstalledSoftware installed, SoftwareProduct? product) => product?.LatestNormalizedVersion is null || installed.NormalizedVersion is null ? InstalledUpdateStatus.Unknown : comparer.Compare(installed.NormalizedVersion, product.LatestNormalizedVersion) switch { VersionComparisonResult.Older => InstalledUpdateStatus.UpdateAvailable, VersionComparisonResult.Equal => InstalledUpdateStatus.UpToDate, VersionComparisonResult.Newer => InstalledUpdateStatus.InstalledNewer, _ => InstalledUpdateStatus.Unknown };
}

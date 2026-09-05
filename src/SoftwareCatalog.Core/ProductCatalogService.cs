using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed class ProductCatalogService(IScanCatalogRepository installers, IProductCatalogRepository products, ProductNormalizer normalizer, ProductMatchingService matching, VersionComparer comparer)
{
    public async Task RegroupAsync(CancellationToken cancellationToken)
    {
        var files = (await installers.GetInstallersAsync(cancellationToken)).ToList();
        var productById = (await products.GetProductsAsync(cancellationToken)).ToDictionary(product => product.Id);
        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            if (!file.Exists) continue;
            cancellationToken.ThrowIfCancellationRequested();
            if (file.ProductId is not null) continue;
            var match = matching.FindMatch(file, files.Where(x => x.ProductId is not null));
            Guid productId;
            if (match?.ProductId is { } existing) productId = existing;
            else
            {
                var name = file.ProductName ?? Path.GetFileNameWithoutExtension(file.FileName);
                var now = DateTimeOffset.UtcNow; var product = new SoftwareProduct(Guid.NewGuid(), name, file.Publisher, normalizer.Normalize(name), now, now);
                product = await products.UpsertProductAsync(product, cancellationToken); productId = product.Id; productById[productId] = product;
            }
            var source = match?.Source ?? ProductMatchSource.FilenameFallback; var confidence = match?.Confidence ?? ProductMatchConfidence.Low;
            await products.LinkInstallerAsync(file.Id, productId, source, confidence, cancellationToken);
            files[index] = file with { ProductId = productId, ProductMatchSource = source, ProductMatchConfidence = confidence };
        }
        foreach (var group in files.Where(file => file.Exists && file.ProductId is not null).GroupBy(file => file.ProductId!.Value))
        {
            if (!productById.TryGetValue(group.Key, out var product)) continue;
            InstallerFile? latest = null;
            foreach (var candidate in group.Where(file => !string.IsNullOrWhiteSpace(file.NormalizedVersion))) if (latest is null || comparer.Compare(latest.NormalizedVersion, candidate.NormalizedVersion) == VersionComparisonResult.Older) latest = candidate;
            if (latest is not null) await products.UpsertProductAsync(product with { LatestLocalVersion = latest.ProductVersion, LatestNormalizedVersion = latest.NormalizedVersion }, cancellationToken);
        }
    }
}

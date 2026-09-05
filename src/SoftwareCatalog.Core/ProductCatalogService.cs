using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed class ProductCatalogService(IScanCatalogRepository installers, IProductCatalogRepository products, ProductNormalizer normalizer, ProductMatchingService matching)
{
    public async Task RegroupAsync(CancellationToken cancellationToken)
    {
        var files = await installers.GetInstallersAsync(cancellationToken);
        foreach (var file in files.Where(f => f.Exists))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (file.ProductId is not null) continue;
            var match = matching.FindMatch(file, files.Where(x => x.ProductId is not null));
            Guid productId;
            if (match?.ProductId is { } existing) productId = existing;
            else
            {
                var name = file.ProductName ?? Path.GetFileNameWithoutExtension(file.FileName);
                var now = DateTimeOffset.UtcNow; var product = new SoftwareProduct(Guid.NewGuid(), name, file.Publisher, normalizer.Normalize(name), now, now, file.ProductVersion, file.NormalizedVersion);
                await products.UpsertProductAsync(product, cancellationToken); productId = product.Id;
            }
            await products.LinkInstallerAsync(file.Id, productId, match?.Source ?? ProductMatchSource.FilenameFallback, match?.Confidence ?? ProductMatchConfidence.Low, cancellationToken);
        }
    }
}

using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed class ProductMatchingService(ProductNormalizer normalizer)
{
    public ProductMatch? FindMatch(InstallerFile file, IEnumerable<InstallerFile> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(file.UpgradeCode) && file.UpgradeCode == candidate.UpgradeCode) return new(candidate.ProductId, ProductMatchSource.MsiUpgradeCode, ProductMatchConfidence.High);
            if (!string.IsNullOrWhiteSpace(file.ProductCode) && file.ProductCode == candidate.ProductCode) return new(candidate.ProductId, ProductMatchSource.MsiProductCode, ProductMatchConfidence.High);
            if (!string.IsNullOrWhiteSpace(file.MsixIdentityName) && file.MsixIdentityName == candidate.MsixIdentityName) return new(candidate.ProductId, ProductMatchSource.MsixIdentity, ProductMatchConfidence.High);
            if (!string.IsNullOrWhiteSpace(file.ProductName) && !string.IsNullOrWhiteSpace(file.Publisher) && normalizer.Normalize(file.ProductName) == normalizer.Normalize(candidate.ProductName) && normalizer.Normalize(file.Publisher) == normalizer.Normalize(candidate.Publisher)) return new(candidate.ProductId, ProductMatchSource.NameAndPublisher, ProductMatchConfidence.Medium);
        }
        return null;
    }
}
public sealed record ProductMatch(Guid? ProductId, ProductMatchSource Source, ProductMatchConfidence Confidence);

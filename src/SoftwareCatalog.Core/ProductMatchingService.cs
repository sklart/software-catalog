using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed class ProductMatchingService(ProductNormalizer normalizer)
{
    public bool HasConflictingStableIdentifier(InstallerFile file, IEnumerable<InstallerFile> candidates)
    {
        var known = candidates.ToArray();
        return !known.Any(candidate => Same(file.UpgradeCode, candidate.UpgradeCode) || Same(file.ProductCode, candidate.ProductCode) || Same(file.MsixIdentityName, candidate.MsixIdentityName)) && known.Any(candidate => Different(file.UpgradeCode, candidate.UpgradeCode) || Different(file.ProductCode, candidate.ProductCode) || Different(file.MsixIdentityName, candidate.MsixIdentityName));
    }

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
    private static bool Different(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && !left.Equals(right, StringComparison.OrdinalIgnoreCase);
    private static bool Same(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && left.Equals(right, StringComparison.OrdinalIgnoreCase);
}
public sealed record ProductMatch(Guid? ProductId, ProductMatchSource Source, ProductMatchConfidence Confidence);

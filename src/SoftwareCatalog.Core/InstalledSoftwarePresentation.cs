using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core;

public sealed record InstalledSoftwareRow(InstalledSoftware Installed, string? MatchedProduct, string? LatestVersion, InstalledUpdateStatus UpdateStatus)
{
    public long Id => Installed.Id;
    public string Name => Installed.DisplayName;
    public string? InstalledVersion => Installed.DisplayVersion;
    public string? Publisher => Installed.Publisher;
    public string? Architecture => Installed.Architecture;
    public InstalledSoftwareSource Source => Installed.Source;
    public string? InstallLocation => Installed.InstallLocation;
    public Guid? ProductId => Installed.ProductId;
    public InstalledSoftwareMatchSource? MatchSource => Installed.MatchSource;
    public InstalledSoftwareMatchConfidence? MatchConfidence => Installed.MatchConfidence;
    public bool Exists => Installed.Exists;
}

public sealed class InstalledSoftwarePresentationService(VersionComparer comparer)
{
    public IReadOnlyList<InstalledSoftwareRow> CreateRows(IEnumerable<InstalledSoftware> installed, IEnumerable<SoftwareProduct> products)
    {
        var byId = products.ToDictionary(product => product.Id);
        return installed.Select(item =>
        {
            byId.TryGetValue(item.ProductId ?? Guid.Empty, out var product);
            var status = product?.LatestNormalizedVersion is null || item.NormalizedVersion is null ? InstalledUpdateStatus.Unknown : comparer.Compare(item.NormalizedVersion, product.LatestNormalizedVersion) switch { VersionComparisonResult.Older => InstalledUpdateStatus.UpdateAvailable, VersionComparisonResult.Equal => InstalledUpdateStatus.UpToDate, VersionComparisonResult.Newer => InstalledUpdateStatus.InstalledNewer, _ => InstalledUpdateStatus.Unknown };
            return new InstalledSoftwareRow(item, product?.CanonicalName, product?.LatestVersion, status);
        }).OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public IReadOnlyDictionary<Guid, SoftwareProduct> ApplyProductSummaries(IEnumerable<SoftwareProduct> products, IEnumerable<InstalledSoftwareRow> rows)
    {
        return products.ToDictionary(product => product.Id, product =>
        {
            var matches = rows.Where(row => row.ProductId == product.Id && row.Exists).ToArray();
            var versions = string.Join(", ", matches.Select(row => row.InstalledVersion).Where(version => !string.IsNullOrWhiteSpace(version)).Distinct(StringComparer.OrdinalIgnoreCase));
            var architectures = string.Join(", ", matches.Select(row => row.Architecture).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase));
            var statuses = matches.Select(row => row.UpdateStatus).ToArray();
            var status = statuses.Contains(InstalledUpdateStatus.UpdateAvailable) ? InstalledUpdateStatus.UpdateAvailable : statuses.Contains(InstalledUpdateStatus.InstalledNewer) ? InstalledUpdateStatus.InstalledNewer : statuses.Contains(InstalledUpdateStatus.UpToDate) ? InstalledUpdateStatus.UpToDate : InstalledUpdateStatus.Unknown;
            return product with { IsInstalled = matches.Length > 0, InstalledVersions = string.IsNullOrEmpty(versions) ? null : versions, InstalledCopiesCount = matches.Length, InstalledArchitectures = string.IsNullOrEmpty(architectures) ? null : architectures, InstalledUpdateStatus = status };
        });
    }
}

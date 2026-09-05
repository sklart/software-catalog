using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IScanCatalogRepository
{
    Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken cancellationToken);
    Task<ScanRoot> AddScanRootAsync(string storedPath, ScanRootPathKind pathKind, bool includeSubdirectories, CancellationToken cancellationToken);
    Task UpdateScanRootAsync(long id, string storedPath, ScanRootPathKind pathKind, CancellationToken cancellationToken);
    Task RemoveScanRootAsync(long id, CancellationToken cancellationToken);
    Task<InstallerFile?> FindInstallerAsync(long scanRootId, string relativePath, CancellationToken cancellationToken);
    Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> installers, CancellationToken cancellationToken);
    Task MarkMissingAsync(long scanRootId, DateTimeOffset scanStartedUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken cancellationToken);
}

public interface IProductCatalogRepository
{
    Task<IReadOnlyList<SoftwareProduct>> GetProductsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallerFile>> GetInstallersForProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<SoftwareProduct> UpsertProductAsync(SoftwareProduct product, CancellationToken cancellationToken);
    Task LinkInstallerAsync(long installerId, Guid productId, ProductMatchSource source, ProductMatchConfidence confidence, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductUpdateSource>> GetUpdateSourcesAsync(Guid productId, CancellationToken cancellationToken);
    Task SetUpdateSourceAsync(ProductUpdateSource source, CancellationToken cancellationToken);
    Task ClearUpdateSourcesAsync(Guid productId, string providerType, CancellationToken cancellationToken);
    Task SaveUpdateCheckAsync(Guid productId, UpdateCheckResult result, CancellationToken cancellationToken);
}

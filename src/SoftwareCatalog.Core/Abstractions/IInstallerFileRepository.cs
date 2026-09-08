using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Abstractions;

public interface IScanCatalogRepository
{
    Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken cancellationToken);
    Task<ScanRoot> AddScanRootAsync(string storedPath, ScanRootPathKind pathKind, bool includeSubdirectories, CancellationToken cancellationToken);
    Task<ScanRoot> EnsureManagedScanRootAsync(ScanRootRole role, string storedPath, ScanRootPathKind pathKind, CancellationToken cancellationToken) => throw new NotSupportedException("Managed storage is not supported by this repository.");
    Task UpdateScanRootAsync(long id, string storedPath, ScanRootPathKind pathKind, CancellationToken cancellationToken);
    Task RemoveScanRootAsync(long id, CancellationToken cancellationToken);
    Task<InstallerFile?> FindInstallerAsync(long scanRootId, string relativePath, CancellationToken cancellationToken);
    Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> installers, CancellationToken cancellationToken);
    Task MarkMissingAsync(long scanRootId, DateTimeOffset scanStartedUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken cancellationToken);
    Task UpdateInstallerStorageAsync(InstallerStorageUpdate update, CancellationToken cancellationToken) => throw new NotSupportedException("Archive storage is not supported by this repository.");
    Task SetInstallerPinnedAsync(long installerId, bool isPinned, CancellationToken cancellationToken) => throw new NotSupportedException("Archive storage is not supported by this repository.");
    Task MarkInstallerPurgedAsync(long installerId, CancellationToken cancellationToken) => throw new NotSupportedException("Archive storage is not supported by this repository.");
    Task SaveArchiveOperationAsync(ArchiveOperation operation, CancellationToken cancellationToken) => throw new NotSupportedException("Archive storage is not supported by this repository.");
    Task<IReadOnlyList<ArchiveOperation>> GetArchiveOperationsAsync(long? installerId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ArchiveOperation>>([]);
}

public interface IArchiveLocationResolver
{
    string ArchiveRoot { get; }
    string TrashRoot { get; }
    string ArchiveStoredPath { get; }
    ScanRootPathKind PathKind { get; }
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
    Task<IReadOnlyList<ProductAlias>> GetProductAliasesAsync(Guid productId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProductAlias>>([]);
    Task SetProductAliasAsync(ProductAlias alias, CancellationToken cancellationToken) => Task.CompletedTask;
    Task RemoveProductAliasAsync(Guid productId, string normalizedAlias, CancellationToken cancellationToken) => Task.CompletedTask;
    Task<IReadOnlyList<UpdateCandidate>> SearchUpdateCandidatesAsync(Guid productId, bool force, int cacheHours, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UpdateCandidate>>([]);
    Task<bool> IsUpdateCandidateCacheFreshAsync(Guid productId, int cacheHours, CancellationToken cancellationToken) => Task.FromResult(false);
    Task SaveUpdateCandidatesAsync(Guid productId, IReadOnlyList<UpdateCandidate> candidates, DateTimeOffset resolvedUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    Task SaveUpdateCheckAsync(Guid productId, UpdateCheckResult result, CancellationToken cancellationToken);
}

public interface IInstalledSoftwareRepository
{
    Task<IReadOnlyList<InstalledSoftware>> GetInstalledSoftwareAsync(CancellationToken cancellationToken);
    Task UpsertInstalledSoftwareAsync(IReadOnlyList<InstalledSoftware> software, DateTimeOffset refreshStartedUtc, CancellationToken cancellationToken);
    Task SetInstalledSoftwareBindingAsync(long installedSoftwareId, Guid? productId, InstalledSoftwareMatchSource? source, InstalledSoftwareMatchConfidence? confidence, CancellationToken cancellationToken);
}

public interface IInstalledSoftwareSource
{
    Task<IReadOnlyList<InstalledSoftware>> ReadAsync(CancellationToken cancellationToken);
}

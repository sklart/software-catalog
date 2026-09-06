using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Infrastructure.Settings;
using SoftwareCatalog.UI.ViewModels;

namespace SoftwareCatalog.UI.Tests;

public sealed class MainViewModelStorageCommandTests
{
    [Theory]
    [InlineData(InstallerStorageState.Active, false, true, false, true, false)]
    [InlineData(InstallerStorageState.Archived, false, false, true, true, false)]
    [InlineData(InstallerStorageState.Trashed, false, false, true, false, true)]
    [InlineData(InstallerStorageState.Active, true, false, false, false, false)]
    [InlineData(InstallerStorageState.Archived, true, false, true, false, false)]
    [InlineData(InstallerStorageState.Trashed, true, false, true, false, false)]
    public void StorageCommandsFollowStateAndPin(InstallerStorageState state, bool pinned, bool archive, bool restore, bool trash, bool purge)
    {
        var vm = Create(); vm.SelectedFile = File(state, pinned);
        Assert.Equal(archive, vm.ArchiveCommand.CanExecute(null)); Assert.Equal(restore, vm.RestoreCommand.CanExecute(null)); Assert.Equal(trash, vm.TrashCommand.CanExecute(null)); Assert.Equal(purge, vm.PurgeCommand.CanExecute(null));
    }
    private static MainViewModel Create()
    {
        var repo = new Repo(); return new MainViewModel(repo, repo, new Resolver(), null!, null!, null!, AppSettings.Default, null!, null!, null!, null!, null!, new InstallerRetentionPlanner(new VersionComparer(), new DuplicateInstallerService()));
    }
    private static InstallerFile File(InstallerStorageState state, bool pinned) { var now = DateTimeOffset.UtcNow; return new InstallerFile(1, 1, "tool.exe", "tool.exe", ".exe", 1, now, null, now, now, true, StorageState:state, IsPinned:pinned); }
    private sealed class Resolver : IPortablePathResolver { public string Resolve(ScanRoot root) => root.StoredPath; public string ToStoredPath(string path, ScanRootPathKind kind) => path; public string GetRelativePath(ScanRoot root, string path) => path; public ScanRootAvailability GetAvailability(ScanRoot root) => ScanRootAvailability.Available; }
    private sealed class Repo : IScanCatalogRepository, IProductCatalogRepository
    {
        public Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken t) => Task.FromResult<IReadOnlyList<ScanRoot>>([]); public Task<ScanRoot> AddScanRootAsync(string s, ScanRootPathKind k, bool b, CancellationToken t) => throw new NotSupportedException(); public Task UpdateScanRootAsync(long i,string s,ScanRootPathKind k,CancellationToken t)=>Task.CompletedTask; public Task RemoveScanRootAsync(long i,CancellationToken t)=>Task.CompletedTask; public Task<InstallerFile?> FindInstallerAsync(long i,string s,CancellationToken t)=>Task.FromResult<InstallerFile?>(null); public Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> f,CancellationToken t)=>Task.CompletedTask; public Task MarkMissingAsync(long i,DateTimeOffset d,CancellationToken t)=>Task.CompletedTask; public Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>([]);
        public Task<IReadOnlyList<SoftwareProduct>> GetProductsAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<SoftwareProduct>>([]); public Task<IReadOnlyList<InstallerFile>> GetInstallersForProductAsync(Guid i,CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>([]); public Task<SoftwareProduct> UpsertProductAsync(SoftwareProduct p,CancellationToken t)=>Task.FromResult(p); public Task LinkInstallerAsync(long i,Guid p,ProductMatchSource s,ProductMatchConfidence c,CancellationToken t)=>Task.CompletedTask; public Task<IReadOnlyList<ProductUpdateSource>> GetUpdateSourcesAsync(Guid i,CancellationToken t)=>Task.FromResult<IReadOnlyList<ProductUpdateSource>>([]); public Task SetUpdateSourceAsync(ProductUpdateSource s,CancellationToken t)=>Task.CompletedTask; public Task ClearUpdateSourcesAsync(Guid i,string s,CancellationToken t)=>Task.CompletedTask; public Task SaveUpdateCheckAsync(Guid i,UpdateCheckResult r,CancellationToken t)=>Task.CompletedTask;
    }
}

using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Infrastructure.Settings;
using SoftwareCatalog.UI.ViewModels;
using System.Windows.Data;

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
    [Theory]
    [InlineData(InstallerStorageState.Active)]
    [InlineData(InstallerStorageState.Archived)]
    [InlineData(InstallerStorageState.Trashed)]
    public void BusyStateDisablesEveryStorageCommand(InstallerStorageState state)
    {
        var vm = Create(); vm.SelectedFile = File(state, false); vm.SetBusyForTesting(true);
        Assert.False(vm.ArchiveCommand.CanExecute(null)); Assert.False(vm.RestoreCommand.CanExecute(null)); Assert.False(vm.TrashCommand.CanExecute(null)); Assert.False(vm.PurgeCommand.CanExecute(null));
    }
    [Fact]
    public void ManagedRootsAreHiddenAndCannotBeRemoved()
    {
        var repo = new Repo(); var now = DateTimeOffset.UtcNow;
        var user = new ScanRoot(1, "User", ScanRootPathKind.Absolute, true, true, now, now, ScanRootRole.User);
        var archive = new ScanRoot(2, "Archive", ScanRootPathKind.Absolute, true, true, now, now, ScanRootRole.Archive);
        var trash = new ScanRoot(3, "Trash", ScanRootPathKind.Absolute, true, true, now, now, ScanRootRole.Trash);
        repo.Roots.AddRange([user, archive, trash]); var vm = Create(repo);
        Assert.Equal([user], vm.ScanRoots); vm.SelectedScanRoot = user; Assert.True(vm.RemoveFolderCommand.CanExecute(null));
        vm.SelectedScanRoot = archive; Assert.False(vm.RemoveFolderCommand.CanExecute(null)); vm.SelectedScanRoot = trash; Assert.False(vm.RemoveFolderCommand.CanExecute(null));
    }
    [Fact]
    public async Task ProductFilesAreGroupedByNormalizedVersion()
    {
        var repo = new Repo(); var product = Product(); repo.ProductFiles.AddRange([File(InstallerStorageState.Active, false) with { Id = 1, ProductId = product.Id, ProductVersion = "3.0", NormalizedVersion = "3.0", Architecture = "x64" }, File(InstallerStorageState.Active, false) with { Id = 2, ProductId = product.Id, ProductVersion = "3.0", NormalizedVersion = "3.0", Architecture = "x86" }, File(InstallerStorageState.Active, false) with { Id = 3, ProductId = product.Id, ProductVersion = "2.0", NormalizedVersion = "2.0", Architecture = "x64" }]);
        var vm = Create(repo); vm.SelectedProduct = product; await vm.RefreshProductFilesForTestingAsync();
        var view = CollectionViewSource.GetDefaultView(vm.ProductFiles); Assert.Contains(view.GroupDescriptions, x => x is PropertyGroupDescription { PropertyName: nameof(InstallerFile.NormalizedVersion) });
        var groups = view.Groups!.Cast<CollectionViewGroup>().OrderBy(x => x.Name).ToList(); Assert.Equal(2, groups.Count); Assert.Equal(1, groups[0].ItemCount); Assert.Equal(2, groups[1].ItemCount);
    }
    [Fact]
    public async Task PurgedTombstoneKeepsProductAndFileInArchiveHistory()
    {
        var repo = new Repo(); var product = Guid.NewGuid(); var tombstone = File(InstallerStorageState.Trashed, false) with { Exists = false, ProductName = "Tool", ProductId = product };
        repo.Installers.Add(tombstone); repo.Operations.Add(new ArchiveOperation(Guid.NewGuid(), tombstone.Id, product, ArchiveOperationType.Purge, InstallerStorageState.Trashed, null, "source", null, tombstone.Sha256, ArchiveOperationStatus.Completed, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var vm = Create(repo); await vm.RefreshArchiveOperationsAsync(); var row = Assert.Single(vm.ArchiveHistoryRows); Assert.Equal("Tool", row.Product); Assert.Equal("tool.exe", row.File);
    }
    private static MainViewModel Create()
    {
        return Create(new Repo());
    }
    private static MainViewModel Create(Repo repo) => new(repo, repo, new Resolver(), null!, null!, null!, AppSettings.Default, null!, null!, null!, null!, null!, new InstallerRetentionPlanner(new VersionComparer(), new DuplicateInstallerService()));
    private static SoftwareProduct Product() { var now = DateTimeOffset.UtcNow; return new SoftwareProduct(Guid.NewGuid(), "Tool", null, "tool", now, now); }
    private static InstallerFile File(InstallerStorageState state, bool pinned) { var now = DateTimeOffset.UtcNow; return new InstallerFile(1, 1, "tool.exe", "tool.exe", ".exe", 1, now, null, now, now, true, StorageState:state, IsPinned:pinned); }
    private sealed class Resolver : IPortablePathResolver { public string Resolve(ScanRoot root) => root.StoredPath; public string ToStoredPath(string path, ScanRootPathKind kind) => path; public string GetRelativePath(ScanRoot root, string path) => path; public ScanRootAvailability GetAvailability(ScanRoot root) => ScanRootAvailability.Available; }
    private sealed class Repo : IScanCatalogRepository, IProductCatalogRepository
    {
        public List<ScanRoot> Roots { get; } = []; public List<InstallerFile> Installers { get; } = []; public List<InstallerFile> ProductFiles { get; } = []; public List<ArchiveOperation> Operations { get; } = [];
        public Task<IReadOnlyList<ScanRoot>> GetScanRootsAsync(CancellationToken t) => Task.FromResult<IReadOnlyList<ScanRoot>>(Roots); public Task<ScanRoot> AddScanRootAsync(string s, ScanRootPathKind k, bool b, CancellationToken t) => throw new NotSupportedException(); public Task UpdateScanRootAsync(long i,string s,ScanRootPathKind k,CancellationToken t)=>Task.CompletedTask; public Task RemoveScanRootAsync(long i,CancellationToken t)=>Task.CompletedTask; public Task<InstallerFile?> FindInstallerAsync(long i,string s,CancellationToken t)=>Task.FromResult<InstallerFile?>(null); public Task UpsertInstallersAsync(IReadOnlyList<InstallerFile> f,CancellationToken t)=>Task.CompletedTask; public Task MarkMissingAsync(long i,DateTimeOffset d,CancellationToken t)=>Task.CompletedTask; public Task<IReadOnlyList<InstallerFile>> GetInstallersAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>(Installers); public Task<IReadOnlyList<ArchiveOperation>> GetArchiveOperationsAsync(long? id,CancellationToken t)=>Task.FromResult<IReadOnlyList<ArchiveOperation>>(Operations);
        public Task<IReadOnlyList<SoftwareProduct>> GetProductsAsync(CancellationToken t)=>Task.FromResult<IReadOnlyList<SoftwareProduct>>([]); public Task<IReadOnlyList<InstallerFile>> GetInstallersForProductAsync(Guid i,CancellationToken t)=>Task.FromResult<IReadOnlyList<InstallerFile>>(ProductFiles.Where(x => x.ProductId == i).ToList()); public Task<SoftwareProduct> UpsertProductAsync(SoftwareProduct p,CancellationToken t)=>Task.FromResult(p); public Task LinkInstallerAsync(long i,Guid p,ProductMatchSource s,ProductMatchConfidence c,CancellationToken t)=>Task.CompletedTask; public Task<IReadOnlyList<ProductUpdateSource>> GetUpdateSourcesAsync(Guid i,CancellationToken t)=>Task.FromResult<IReadOnlyList<ProductUpdateSource>>([]); public Task SetUpdateSourceAsync(ProductUpdateSource s,CancellationToken t)=>Task.CompletedTask; public Task ClearUpdateSourcesAsync(Guid i,string s,CancellationToken t)=>Task.CompletedTask; public Task SaveUpdateCheckAsync(Guid i,UpdateCheckResult r,CancellationToken t)=>Task.CompletedTask;
    }
}

using SoftwareCatalog.Core;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Core.Tests;

public sealed class InstalledSoftwareMatchingServiceTests
{
    [Fact]
    public async Task ExactNameAndPublisherIsBound()
    {
        var product = Product("Contoso Tool", "Contoso"); var inventory = new MemoryInventory(Installed("Contoso Tool", "Contoso"));
        await new InstalledSoftwareMatchingService(new ProductNormalizer()).MatchAsync(inventory, new MemoryCatalog(product), CancellationToken.None);
        Assert.Equal(product.Id, inventory.BoundProductId); Assert.Equal(InstalledSoftwareMatchSource.NameAndPublisher, inventory.Source);
    }
    [Fact]
    public async Task AliasIsBoundButNameOnlyIsManualReview()
    {
        var product=Product("Contoso Tool", "Contoso"); var alias=new ProductAlias(product.Id,"Tool Legacy","toollegacy"); var inventory=new MemoryInventory(Installed("Tool Legacy", null));
        await new InstalledSoftwareMatchingService(new ProductNormalizer()).MatchAsync(inventory,new MemoryCatalog(product,alias),CancellationToken.None);
        Assert.Equal(product.Id,inventory.BoundProductId); Assert.Equal(InstalledSoftwareMatchSource.Alias,inventory.Source);
        var nameOnly=new MemoryInventory(Installed("Contoso Tool",null)); await new InstalledSoftwareMatchingService(new ProductNormalizer()).MatchAsync(nameOnly,new MemoryCatalog(product),CancellationToken.None);
        Assert.Null(nameOnly.BoundProductId); Assert.Equal(InstalledSoftwareMatchSource.ManualReview,nameOnly.Source);
    }
    [Theory]
    [InlineData("1.0", "2.0", InstalledUpdateStatus.UpdateAvailable)]
    [InlineData("2.0", "2.0", InstalledUpdateStatus.UpToDate)]
    [InlineData("3.0", "2.0", InstalledUpdateStatus.InstalledNewer)]
    public void ComparesInstalledVersion(string installed, string latest, InstalledUpdateStatus expected) => Assert.Equal(expected, new InstalledSoftwareInventoryService([], null!, null!, null!, new VersionComparer()).GetUpdateStatus(Installed("Tool", null) with { NormalizedVersion=installed }, Product("Tool", null) with { LatestNormalizedVersion=latest }));

    private static SoftwareProduct Product(string name,string? publisher) { var now=DateTimeOffset.UtcNow; return new(Guid.NewGuid(),name,publisher,new ProductNormalizer().Normalize(name),now,now); }
    private static InstalledSoftware Installed(string name,string? publisher) { var now=DateTimeOffset.UtcNow; return new(1,name,"1.0",publisher,new ProductNormalizer().Normalize(name),"1.0",null,null,"x64",InstalledSoftwareSource.Hklm64Uninstall,null,null,null,null,now,now,true); }
    private sealed class MemoryInventory(InstalledSoftware item) : IInstalledSoftwareRepository
    {
        public Guid? BoundProductId { get; private set; } public InstalledSoftwareMatchSource? Source { get; private set; }
        public Task<IReadOnlyList<InstalledSoftware>> GetInstalledSoftwareAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<InstalledSoftware>>([item]);
        public Task UpsertInstalledSoftwareAsync(IReadOnlyList<InstalledSoftware> software,DateTimeOffset when,CancellationToken token)=>Task.CompletedTask;
        public Task SetInstalledSoftwareBindingAsync(long id,Guid? productId,InstalledSoftwareMatchSource? source,InstalledSoftwareMatchConfidence? confidence,CancellationToken token){BoundProductId=productId;Source=source;return Task.CompletedTask;}
    }
    private sealed class MemoryCatalog(SoftwareProduct product, params ProductAlias[] aliases) : IProductCatalogRepository
    {
        public Task<IReadOnlyList<SoftwareProduct>> GetProductsAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<SoftwareProduct>>([product]);
        public Task<IReadOnlyList<ProductAlias>> GetProductAliasesAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<ProductAlias>>(aliases);
        public Task<IReadOnlyList<InstallerFile>> GetInstallersForProductAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<InstallerFile>>([]);
        public Task<SoftwareProduct> UpsertProductAsync(SoftwareProduct p,CancellationToken token)=>Task.FromResult(p);
        public Task LinkInstallerAsync(long id,Guid p,ProductMatchSource s,ProductMatchConfidence c,CancellationToken token)=>Task.CompletedTask;
        public Task<IReadOnlyList<ProductUpdateSource>> GetUpdateSourcesAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<ProductUpdateSource>>([]);
        public Task SetUpdateSourceAsync(ProductUpdateSource source,CancellationToken token)=>Task.CompletedTask;
        public Task ClearUpdateSourcesAsync(Guid id,string provider,CancellationToken token)=>Task.CompletedTask;
        public Task SaveUpdateCheckAsync(Guid id,UpdateCheckResult result,CancellationToken token)=>Task.CompletedTask;
    }
}

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
    [Fact]
    public async Task LocalExternalIdentityWinsButUpdateSourceIdentityIsIgnored()
    {
        var product=Product("Different name",null) with { ExternalProductId="Contoso.Tool" }; var inventory=new MemoryInventory(Installed("Tool",null) with { ExternalId="Contoso.Tool" }); var service=new InstalledSoftwareMatchingService(new ProductNormalizer());
        await service.MatchAsync(inventory,new MemoryCatalog(product),CancellationToken.None); Assert.Null(inventory.BoundProductId); Assert.Equal(InstalledSoftwareMatchSource.ManualReview,inventory.Source);
        var installer = new InstallerFile(1, 1, "tool.msi", "tool.msi", ".msi", 1, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, ProductCode: "{LOCAL-PRODUCT-CODE}");
        inventory=new MemoryInventory(Installed("Tool",null) with { ExternalId="{LOCAL-PRODUCT-CODE}" });
        await service.MatchAsync(inventory,new MemoryCatalog([product], [], [installer]),CancellationToken.None); Assert.Equal(product.Id,inventory.BoundProductId); Assert.Equal(InstalledSoftwareMatchSource.ExternalIdentity,inventory.Source);
        var manual=new MemoryInventory(Installed("Other",null) with { ProductId=product.Id, MatchSource=InstalledSoftwareMatchSource.Manual, MatchConfidence=InstalledSoftwareMatchConfidence.Exact }); await service.MatchAsync(manual,new MemoryCatalog(Product("Other",null)),CancellationToken.None); Assert.Null(manual.Source);
    }
    [Fact]
    public async Task AmbiguousAndLowCandidatesAreNotAutoBound()
    {
        var first=Product("Tool",null); var second=Product("Tool",null); var ambiguous=new MemoryInventory(Installed("Tool",null)); await new InstalledSoftwareMatchingService(new ProductNormalizer()).MatchAsync(ambiguous,new MemoryCatalog([first,second]),CancellationToken.None); Assert.Null(ambiguous.BoundProductId); Assert.Equal(InstalledSoftwareMatchConfidence.Ambiguous,ambiguous.Confidence);
        var low=new MemoryInventory(Installed("Tool",null)); await new InstalledSoftwareMatchingService(new ProductNormalizer()).MatchAsync(low,new MemoryCatalog(Product("Tool",null)),CancellationToken.None); Assert.Null(low.BoundProductId); Assert.Equal(InstalledSoftwareMatchSource.ManualReview,low.Source);
    }
    [Theory]
    [InlineData("1.0", "2.0", InstalledUpdateStatus.UpdateAvailable)]
    [InlineData("2.0", "2.0", InstalledUpdateStatus.UpToDate)]
    [InlineData("3.0", "2.0", InstalledUpdateStatus.InstalledNewer)]
    public void ComparesInstalledVersion(string installed, string latest, InstalledUpdateStatus expected) => Assert.Equal(expected, new InstalledSoftwareInventoryService([], null!, null!, null!, new VersionComparer()).GetUpdateStatus(Installed("Tool", null) with { NormalizedVersion=installed }, Product("Tool", null) with { LatestNormalizedVersion=latest }));
    [Fact]
    public void PresentationProvidesStatusAndProductSummary()
    {
        var product=Product("Tool","Contoso") with { LatestVersion="2.0", LatestNormalizedVersion="2.0" }; var installed=Installed("Tool","Contoso") with { ProductId=product.Id, NormalizedVersion="1.0", Architecture="x64" }; var presentation=new InstalledSoftwarePresentationService(new VersionComparer()); var rows=presentation.CreateRows([installed],[product]); var summary=presentation.ApplyProductSummaries([product],rows)[product.Id];
        Assert.Equal(InstalledUpdateStatus.UpdateAvailable,Assert.Single(rows).UpdateStatus); Assert.True(summary.IsInstalled); Assert.Equal(1,summary.InstalledCopiesCount); Assert.Equal("1.0",summary.InstalledVersions); Assert.Equal("x64",summary.InstalledArchitectures);
    }
    [Fact]
    public async Task InventoryDeduplicationUsesIdentityAndKeepsAmbiguousExternalIdentitiesSeparate()
    {
        var first=Installed("Tool","Vendor") with { ExternalId="Vendor.Tool", InstallLocation=@"C:\Tool" }; var duplicate=first with { Source=InstalledSoftwareSource.Hklm32Uninstall }; var different=first with { ExternalId="Vendor.Tool.Other" }; var repository=new CaptureInventory(); var source=new StaticSource([first,duplicate,different]);
        await new InstalledSoftwareInventoryService([source],repository,new MemoryCatalog(Array.Empty<SoftwareProduct>()),new InstalledSoftwareMatchingService(new ProductNormalizer()),new VersionComparer()).RefreshAsync(CancellationToken.None);
        Assert.Equal(2,repository.Upserted.Count); Assert.Contains(repository.Upserted,item=>item.ExternalId=="Vendor.Tool"); Assert.Contains(repository.Upserted,item=>item.ExternalId=="Vendor.Tool.Other");
    }

    private static SoftwareProduct Product(string name,string? publisher) { var now=DateTimeOffset.UtcNow; return new(Guid.NewGuid(),name,publisher,new ProductNormalizer().Normalize(name),now,now); }
    private static InstalledSoftware Installed(string name,string? publisher) { var now=DateTimeOffset.UtcNow; return new(1,name,"1.0",publisher,new ProductNormalizer().Normalize(name),"1.0",null,null,"x64",InstalledSoftwareSource.Hklm64Uninstall,null,null,null,null,now,now,true); }
    private sealed class MemoryInventory(InstalledSoftware item) : IInstalledSoftwareRepository
    {
        public Guid? BoundProductId { get; private set; } public InstalledSoftwareMatchSource? Source { get; private set; } public InstalledSoftwareMatchConfidence? Confidence { get; private set; }
        public Task<IReadOnlyList<InstalledSoftware>> GetInstalledSoftwareAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<InstalledSoftware>>([item]);
        public Task UpsertInstalledSoftwareAsync(IReadOnlyList<InstalledSoftware> software,DateTimeOffset when,CancellationToken token)=>Task.CompletedTask;
        public Task SetInstalledSoftwareBindingAsync(long id,Guid? productId,InstalledSoftwareMatchSource? source,InstalledSoftwareMatchConfidence? confidence,CancellationToken token){BoundProductId=productId;Source=source;Confidence=confidence;return Task.CompletedTask;}
    }
    private sealed class CaptureInventory : IInstalledSoftwareRepository
    {
        public List<InstalledSoftware> Upserted { get; }=[];
        public Task<IReadOnlyList<InstalledSoftware>> GetInstalledSoftwareAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<InstalledSoftware>>(Upserted);
        public Task UpsertInstalledSoftwareAsync(IReadOnlyList<InstalledSoftware> software,DateTimeOffset when,CancellationToken token){Upserted.Clear();Upserted.AddRange(software.Select((item,index)=>item with { Id=index+1 }));return Task.CompletedTask;}
        public Task SetInstalledSoftwareBindingAsync(long id,Guid? productId,InstalledSoftwareMatchSource? source,InstalledSoftwareMatchConfidence? confidence,CancellationToken token)=>Task.CompletedTask;
    }
    private sealed class StaticSource(IReadOnlyList<InstalledSoftware> items) : IInstalledSoftwareSource { public Task<IReadOnlyList<InstalledSoftware>> ReadAsync(CancellationToken token)=>Task.FromResult(items); }
    private sealed class MemoryCatalog : IProductCatalogRepository
    {
        private readonly SoftwareProduct[] _products; private readonly ProductAlias[] _aliases; private readonly InstallerFile[] _installers;
        public MemoryCatalog(SoftwareProduct product,params ProductAlias[] aliases) : this([product],aliases,[]) { } public MemoryCatalog(SoftwareProduct[] products,params ProductAlias[] aliases) : this(products,aliases,[]) { } public MemoryCatalog(SoftwareProduct[] products,ProductAlias[] aliases,InstallerFile[] installers){_products=products;_aliases=aliases;_installers=installers;}
        public Task<IReadOnlyList<SoftwareProduct>> GetProductsAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<SoftwareProduct>>(_products);
        public Task<IReadOnlyList<ProductAlias>> GetProductAliasesAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<ProductAlias>>(_aliases.Where(alias=>alias.ProductId==id).ToArray());
        public Task<IReadOnlyList<InstallerFile>> GetInstallersForProductAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<InstallerFile>>(_installers.Where(file => file.ProductId is null || file.ProductId == id).ToArray());
        public Task<SoftwareProduct> UpsertProductAsync(SoftwareProduct p,CancellationToken token)=>Task.FromResult(p);
        public Task LinkInstallerAsync(long id,Guid p,ProductMatchSource s,ProductMatchConfidence c,CancellationToken token)=>Task.CompletedTask;
        public Task<IReadOnlyList<ProductUpdateSource>> GetUpdateSourcesAsync(Guid id,CancellationToken token)=>Task.FromResult<IReadOnlyList<ProductUpdateSource>>([]);
        public Task SetUpdateSourceAsync(ProductUpdateSource source,CancellationToken token)=>Task.CompletedTask;
        public Task ClearUpdateSourcesAsync(Guid id,string provider,CancellationToken token)=>Task.CompletedTask;
        public Task SaveUpdateCheckAsync(Guid id,UpdateCheckResult result,CancellationToken token)=>Task.CompletedTask;
    }
}

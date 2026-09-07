using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;

public sealed class Stage3PersistenceTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private CatalogDatabase _database = null!;
    public async Task InitializeAsync() { Directory.CreateDirectory(_folder); _database = new CatalogDatabase(Path.Combine(_folder, "catalog.db")); await _database.InitializeAsync(CancellationToken.None); }
    public Task DisposeAsync() { Directory.Delete(_folder, true); return Task.CompletedTask; }
    [Fact]
    public async Task PersistsMsixIdentityAndProductLink()
    {
        var root = await _database.AddScanRootAsync("C:\\Catalog", ScanRootPathKind.Absolute, true, CancellationToken.None); var now = DateTimeOffset.UtcNow;
        await _database.UpsertInstallersAsync([new InstallerFile(0, root.Id, "sample.msix", "sample.msix", ".msix", 1, now, null, now, now, true, ProductName: "Sample Application", MsixIdentityName: "Contoso.Sample")], CancellationToken.None);
        var stored = Assert.Single(await _database.GetInstallersAsync(CancellationToken.None)); Assert.Equal("Contoso.Sample", stored.MsixIdentityName);
        var product = await _database.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(), "Sample Application", "Contoso", "sampleapplication", now, now, "1.0", "1.0"), CancellationToken.None);
        await _database.LinkInstallerAsync(stored.Id, product.Id, ProductMatchSource.MsixIdentity, ProductMatchConfidence.High, CancellationToken.None);
        var linked = Assert.Single(await _database.GetInstallersForProductAsync(product.Id, CancellationToken.None)); Assert.Equal(product.Id, linked.ProductId); Assert.Equal(ProductMatchSource.MsixIdentity, linked.ProductMatchSource); Assert.Equal(ProductMatchConfidence.High, linked.ProductMatchConfidence);
    }
    [Fact]
    public async Task PersistsAndClearsSourcesAndUpdateState()
    {
        var now = DateTimeOffset.UtcNow; var product = await _database.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(), "Tool", "Publisher", "tool", now, now), CancellationToken.None);
        var expectedSource = new ProductUpdateSource(Guid.NewGuid(), product.Id, "GitHub", "owner/repo", true, true); await _database.SetUpdateSourceAsync(expectedSource, CancellationToken.None);
        var source = Assert.Single(await _database.GetUpdateSourcesAsync(product.Id, CancellationToken.None)); Assert.Equal(expectedSource.Id, source.Id); Assert.Equal(product.Id, source.ProductId); Assert.Equal("GitHub", source.ProviderType); Assert.Equal("owner/repo", source.ExternalId); Assert.True(source.Enabled); Assert.True(source.IsExplicit);
        await _database.SaveUpdateCheckAsync(product.Id, new UpdateCheckResult(UpdateStatus.UpdateAvailable, "2.0", "2.0", Source: "GitHub", ExternalProductId: "owner/repo", Error: "brief", CheckedUtc: now), CancellationToken.None);
        var saved = Assert.Single(await _database.GetProductsAsync(CancellationToken.None)); Assert.Equal(UpdateStatus.UpdateAvailable, saved.UpdateStatus); Assert.Equal("2.0", saved.LatestVersion); Assert.Equal("2.0", saved.LatestNormalizedVersion); Assert.Equal("GitHub", saved.UpdateProvider); Assert.Equal("owner/repo", saved.ExternalProductId); Assert.Equal("brief", saved.UpdateError); Assert.Equal(now, saved.LastCheckedUtc);
        await _database.ClearUpdateSourcesAsync(product.Id, "GitHub", CancellationToken.None); Assert.Empty(await _database.GetUpdateSourcesAsync(product.Id, CancellationToken.None));
    }
    [Fact]
    public async Task MappingTimestampsAndAliasesSurviveRestart()
    {
        var now=DateTimeOffset.Parse("2026-01-02T03:04:05Z"); var product=await _database.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(),"Tool","Publisher","tool",now,now),CancellationToken.None);
        await _database.SetUpdateSourceAsync(new ProductUpdateSource(Guid.NewGuid(),product.Id,"WinGet","Vendor.Tool",true,false,MappingSource.ExactMatch,MappingConfidence.High,now),CancellationToken.None);
        await _database.SetProductAliasAsync(new ProductAlias(product.Id,"Legacy Tool","legacytool",MappingSource.Manual),CancellationToken.None);
        var source=Assert.Single(await _database.GetUpdateSourcesAsync(product.Id,CancellationToken.None)); Assert.NotNull(source.CreatedUtc); Assert.NotNull(source.UpdatedUtc); Assert.Equal(MappingConfidence.High,source.Confidence); Assert.Equal(MappingSource.ExactMatch,source.Source);
        var reopened=new CatalogDatabase(Path.Combine(_folder,"catalog.db")); await reopened.InitializeAsync(CancellationToken.None); var restored=Assert.Single(await reopened.GetUpdateSourcesAsync(product.Id,CancellationToken.None)); Assert.Equal(source.CreatedUtc,restored.CreatedUtc); Assert.Equal(source.UpdatedUtc,restored.UpdatedUtc); Assert.Equal("Legacy Tool",Assert.Single(await reopened.GetProductAliasesAsync(product.Id,CancellationToken.None)).Alias);
    }
    [Fact]
    public async Task CandidateCacheHonorsTtlForPositiveAndNegativeResults()
    {
        var product=await _database.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(),"Tool",null,"tool",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow),CancellationToken.None);
        var candidate=new UpdateCandidate("GitHub","owner/tool","Tool","owner",null,MappingConfidence.High,"exact");
        await _database.SaveUpdateCandidatesAsync(product.Id,[candidate],DateTimeOffset.UtcNow,CancellationToken.None);
        Assert.True(await _database.IsUpdateCandidateCacheFreshAsync(product.Id,1,CancellationToken.None));
        Assert.Equal(candidate,Assert.Single(await _database.SearchUpdateCandidatesAsync(product.Id,false,1,CancellationToken.None)));
        await _database.SaveUpdateCandidatesAsync(product.Id,[candidate],DateTimeOffset.UtcNow.AddHours(-2),CancellationToken.None);
        Assert.False(await _database.IsUpdateCandidateCacheFreshAsync(product.Id,1,CancellationToken.None));
        Assert.Empty(await _database.SearchUpdateCandidatesAsync(product.Id,false,1,CancellationToken.None));
        Assert.Empty(await _database.SearchUpdateCandidatesAsync(product.Id,true,1,CancellationToken.None));
        await _database.SaveUpdateCandidatesAsync(product.Id,[],DateTimeOffset.UtcNow,CancellationToken.None);
        Assert.True(await _database.IsUpdateCandidateCacheFreshAsync(product.Id,1,CancellationToken.None));
        Assert.Empty(await _database.SearchUpdateCandidatesAsync(product.Id,false,1,CancellationToken.None));
    }
}

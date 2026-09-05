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
        var linked = Assert.Single(await _database.GetInstallersForProductAsync(product.Id, CancellationToken.None)); Assert.Equal(ProductMatchSource.MsixIdentity, linked.ProductMatchSource); Assert.Equal(ProductMatchConfidence.High, linked.ProductMatchConfidence);
    }
    [Fact]
    public async Task PersistsAndClearsSourcesAndUpdateState()
    {
        var now = DateTimeOffset.UtcNow; var product = await _database.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(), "Tool", "Publisher", "tool", now, now), CancellationToken.None);
        await _database.SetUpdateSourceAsync(new ProductUpdateSource(Guid.NewGuid(), product.Id, "GitHub", "owner/repo", true, true), CancellationToken.None);
        var source = Assert.Single(await _database.GetUpdateSourcesAsync(product.Id, CancellationToken.None)); Assert.Equal("owner/repo", source.ExternalId); Assert.True(source.IsExplicit);
        await _database.SaveUpdateCheckAsync(product.Id, new UpdateCheckResult(UpdateStatus.UpdateAvailable, "2.0", "2.0", Source: "GitHub", ExternalProductId: "owner/repo", Error: "brief", CheckedUtc: now), CancellationToken.None);
        var saved = Assert.Single(await _database.GetProductsAsync(CancellationToken.None)); Assert.Equal(UpdateStatus.UpdateAvailable, saved.UpdateStatus); Assert.Equal("2.0", saved.LatestVersion); Assert.Equal("GitHub", saved.UpdateProvider);
        await _database.ClearUpdateSourcesAsync(product.Id, "GitHub", CancellationToken.None); Assert.Empty(await _database.GetUpdateSourcesAsync(product.Id, CancellationToken.None));
    }
}

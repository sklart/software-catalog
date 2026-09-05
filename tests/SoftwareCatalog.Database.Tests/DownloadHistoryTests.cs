using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;
using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Tests;

public sealed class DownloadHistoryTests : IAsyncLifetime
{
    private readonly string _folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string Path => System.IO.Path.Combine(_folder, "catalog.db");
    public Task InitializeAsync() { Directory.CreateDirectory(_folder); return Task.CompletedTask; }
    public async Task DisposeAsync() { SqliteConnection.ClearAllPools(); for (var attempt = 0; attempt < 10; attempt++) { try { Directory.Delete(_folder, true); return; } catch (IOException) { GC.Collect(); GC.WaitForPendingFinalizers(); await Task.Delay(50); } } }
    [Fact]
    public async Task PersistsFullHistoryAcrossDatabaseRestart()
    {
        var id = Guid.NewGuid(); var product = Guid.NewGuid(); var started = DateTimeOffset.Parse("2026-01-02T03:04:05Z"); var complete = started.AddMinutes(2);
        var first = new CatalogDatabase(Path); await first.InitializeAsync(CancellationToken.None);
        await ((IDownloadHistoryRepository)first).SaveDownloadHistoryAsync(new(id, product, "WinGet", "Vendor.Tool", "2.0", "tool.exe", "https://example.test/tool.exe", "AA", "BB", DownloadStatus.Completed, null, started, complete, "Downloads/tool.exe"), CancellationToken.None);
        var second = new CatalogDatabase(Path); await second.InitializeAsync(CancellationToken.None); var item = Assert.Single(await ((IDownloadHistoryRepository)second).GetDownloadHistoryAsync(CancellationToken.None));
        Assert.Equal(id, item.Id); Assert.Equal(product, item.ProductId); Assert.Equal("WinGet", item.ProviderType); Assert.Equal("Vendor.Tool", item.ExternalProductId); Assert.Equal("2.0", item.Version); Assert.Equal("tool.exe", item.FileName); Assert.Equal("https://example.test/tool.exe", item.SourceUrl); Assert.Equal("AA", item.ExpectedSha256); Assert.Equal("BB", item.ActualSha256); Assert.Equal(DownloadStatus.Completed, item.Status); Assert.Equal(started, item.StartedUtc); Assert.Equal(complete, item.CompletedUtc); Assert.Equal("Downloads/tool.exe", item.FinalRelativePath);
    }
    [Fact]
    public async Task PersistsCancelledAndErrorAcrossRestart()
    {
        var database = new CatalogDatabase(Path); await database.InitializeAsync(CancellationToken.None); var history = (IDownloadHistoryRepository)database; var product = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await history.SaveDownloadHistoryAsync(new(Guid.NewGuid(), product, "GitHub", "owner/repo", "2.0", "tool.exe", "https://example.test/tool.exe", null, null, DownloadStatus.Cancelled, "Отменено.", now, now, null), CancellationToken.None); await history.SaveDownloadHistoryAsync(new(Guid.NewGuid(), product, "WinGet", "Vendor.Tool", "2.0", "tool.msi", "https://example.test/tool.msi", "AA", null, DownloadStatus.Error, "SHA mismatch", now, now, null), CancellationToken.None);
        var reopened = new CatalogDatabase(Path); await reopened.InitializeAsync(CancellationToken.None); var rows = await ((IDownloadHistoryRepository)reopened).GetDownloadHistoryAsync(CancellationToken.None); Assert.Contains(rows, row => row.Status == DownloadStatus.Cancelled && row.Error == "Отменено."); Assert.Contains(rows, row => row.Status == DownloadStatus.Error && row.ExpectedSha256 == "AA" && row.Error == "SHA mismatch");
    }
    [Fact]
    public async Task MigratesRealV5FixtureToV6WithoutLosingStage3Rows()
    {
        await using (var connection = new SqliteConnection($"Data Source={Path}")) { await connection.OpenAsync(); var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE schema_migrations(version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL); INSERT INTO schema_migrations VALUES(1,'2026-01-01T00:00:00Z'),(2,'2026-01-01T00:00:00Z'),(3,'2026-01-01T00:00:00Z'),(4,'2026-01-01T00:00:00Z'),(5,'2026-01-01T00:00:00Z'); CREATE TABLE installer_files(id INTEGER PRIMARY KEY, file_name TEXT NOT NULL); INSERT INTO installer_files VALUES(7,'tool.msi'); CREATE TABLE software_products(id TEXT PRIMARY KEY, canonical_name TEXT NOT NULL); INSERT INTO software_products VALUES('11111111-1111-1111-1111-111111111111','Tool'); CREATE TABLE product_update_sources(id TEXT PRIMARY KEY, product_id TEXT NOT NULL, provider_type TEXT NOT NULL, external_id TEXT NOT NULL); INSERT INTO product_update_sources VALUES('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','WinGet','Vendor.Tool');"; await command.ExecuteNonQueryAsync(); }
        var database = new CatalogDatabase(Path); await database.InitializeAsync(CancellationToken.None); await database.InitializeAsync(CancellationToken.None);
        await using var verify = new SqliteConnection($"Data Source={Path}"); await verify.OpenAsync(); await using var count = verify.CreateCommand(); count.CommandText = "SELECT COUNT(*) FROM schema_migrations"; Assert.Equal(6L, Convert.ToInt64(await count.ExecuteScalarAsync())); count.CommandText = "SELECT COUNT(*) FROM download_history"; Assert.Equal(0L, Convert.ToInt64(await count.ExecuteScalarAsync())); count.CommandText = "SELECT file_name FROM installer_files WHERE id=7"; Assert.Equal("tool.msi", await count.ExecuteScalarAsync()); count.CommandText = "SELECT canonical_name FROM software_products"; Assert.Equal("Tool", await count.ExecuteScalarAsync()); count.CommandText = "SELECT external_id FROM product_update_sources"; Assert.Equal("Vendor.Tool", await count.ExecuteScalarAsync()); await verify.CloseAsync();
    }
    [Fact]
    public async Task RealV5ToV6PreservesCompleteStageThreeAndUpdateState()
    {
        var original = new CatalogDatabase(Path); await original.InitializeAsync(CancellationToken.None); var now = DateTimeOffset.Parse("2026-01-02T03:04:05Z");
        var root = await original.AddScanRootAsync("C:\\Catalog", ScanRootPathKind.Absolute, true, CancellationToken.None);
        await original.UpsertInstallersAsync([new InstallerFile(0, root.Id, "nested\\Tool.msix", "Tool.msix", ".msix", 42, now, "ABC", now, now, true, InstallerKind.Msix, "Tool", "2.0", "Vendor", "2.0.0", "Tool setup", "x64", MetadataSource.MsixManifest, MetadataStatus.Success, null, "2.0", "product-code", "upgrade-code", "tool.msix", MsixIdentityName: "Contoso.Tool")], CancellationToken.None);
        var product = await original.UpsertProductAsync(new SoftwareProduct(Guid.NewGuid(), "Tool", "Vendor", "tool", now, now), CancellationToken.None); var installer = Assert.Single(await original.GetInstallersAsync(CancellationToken.None)); await original.LinkInstallerAsync(installer.Id, product.Id, ProductMatchSource.MsixIdentity, ProductMatchConfidence.High, CancellationToken.None);
        var source = new ProductUpdateSource(Guid.NewGuid(), product.Id, "WinGet", "Vendor.Tool", true, true); await original.SetUpdateSourceAsync(source, CancellationToken.None); await original.SaveUpdateCheckAsync(product.Id, new(UpdateStatus.UpdateAvailable, "3.0", "3.0", Source: "WinGet", ExternalProductId: "Vendor.Tool", CheckedUtc: now), CancellationToken.None);
        await using (var connection = new SqliteConnection($"Data Source={Path};Pooling=False")) { await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "DROP TABLE download_history; DELETE FROM schema_migrations WHERE version=6;"; await command.ExecuteNonQueryAsync(); }
        var upgraded = new CatalogDatabase(Path); await upgraded.InitializeAsync(CancellationToken.None); await upgraded.InitializeAsync(CancellationToken.None);
        var restored = Assert.Single(await upgraded.GetInstallersForProductAsync(product.Id, CancellationToken.None)); Assert.Equal("nested\\Tool.msix", restored.RelativePath); Assert.Equal("ABC", restored.Sha256); Assert.Equal("Contoso.Tool", restored.MsixIdentityName); Assert.Equal("upgrade-code", restored.UpgradeCode); Assert.Equal(ProductMatchSource.MsixIdentity, restored.ProductMatchSource);
        var restoredProduct = Assert.Single(await upgraded.GetProductsAsync(CancellationToken.None)); Assert.Equal(UpdateStatus.UpdateAvailable, restoredProduct.UpdateStatus); Assert.Equal("3.0", restoredProduct.LatestNormalizedVersion); Assert.Equal("WinGet", restoredProduct.UpdateProvider); Assert.Equal("Vendor.Tool", restoredProduct.ExternalProductId);
        var restoredSource = Assert.Single(await upgraded.GetUpdateSourcesAsync(product.Id, CancellationToken.None)); Assert.Equal(source, restoredSource);
        await using var verify = new SqliteConnection($"Data Source={Path};Pooling=False"); await verify.OpenAsync(); await using var versions = verify.CreateCommand(); versions.CommandText = "SELECT group_concat(version, ',') FROM (SELECT version FROM schema_migrations ORDER BY version)"; Assert.Equal("1,2,3,4,5,6", await versions.ExecuteScalarAsync()); versions.CommandText = "SELECT COUNT(*) FROM download_history"; Assert.Equal(0L, Convert.ToInt64(await versions.ExecuteScalarAsync()));
    }
}

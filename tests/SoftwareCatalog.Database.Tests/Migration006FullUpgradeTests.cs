using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;

public sealed class Migration006FullUpgradeTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SoftwareCatalogMigrationTests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_folder, "v5.db");
    public Task InitializeAsync() { Directory.CreateDirectory(_folder); return Task.CompletedTask; }
    public Task DisposeAsync() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); return Task.CompletedTask; }

    [Fact]
    public async Task ManuallyCreatedFullV5DatabaseUpgradesToV6WithoutChangingAnyStageThreeData()
    {
        const string created = "2026-01-02T03:04:05.0000000+00:00"; const string updated = "2026-02-03T04:05:06.0000000+00:00";
        var productId = Guid.Parse("11111111-1111-1111-1111-111111111111"); var sourceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await CreateFullV5Async(productId, sourceId, created, updated);
        await using (var before = new SqliteConnection($"Data Source={DatabasePath};Pooling=False")) { await before.OpenAsync(); await using var migrations = before.CreateCommand(); migrations.CommandText = "SELECT group_concat(version, ',') FROM (SELECT version FROM schema_migrations ORDER BY version)"; Assert.Equal("1,2,3,4,5", await migrations.ExecuteScalarAsync()); }
        var database = new CatalogDatabase(DatabasePath); await database.InitializeAsync(CancellationToken.None);
        await AssertPreservedAsync(database, productId, sourceId, created, updated); await database.InitializeAsync(CancellationToken.None); await AssertPreservedAsync(database, productId, sourceId, created, updated);
        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False"); await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = "SELECT group_concat(version, ',') FROM (SELECT version FROM schema_migrations ORDER BY version)"; Assert.Equal("1,2,3,4,5,6", await command.ExecuteScalarAsync()); command.CommandText = "SELECT COUNT(*) FROM download_history"; Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private async Task AssertPreservedAsync(CatalogDatabase database, Guid productId, Guid sourceId, string created, string updated)
    {
        var file = Assert.Single(await database.GetInstallersForProductAsync(productId, CancellationToken.None));
        Assert.Equal(42, file.Id); Assert.Equal(7, file.ScanRootId); Assert.Equal("nested\\Tool.msix", file.RelativePath); Assert.Equal("Tool.msix", file.FileName); Assert.Equal(".msix", file.Extension); Assert.Equal(987654, file.Size); Assert.Equal(DateTimeOffset.Parse(created), file.LastWriteTimeUtc); Assert.Equal("ABCDEF", file.Sha256); Assert.Equal(DateTimeOffset.Parse(created), file.FirstSeenUtc); Assert.Equal(DateTimeOffset.Parse(updated), file.LastSeenUtc); Assert.True(file.Exists); Assert.Equal(InstallerKind.Msix, file.InstallerKind); Assert.Equal("Tool", file.ProductName); Assert.Equal("2.0", file.ProductVersion); Assert.Equal("Vendor", file.Publisher); Assert.Equal("2.0.0.0", file.FileVersion); Assert.Equal("Tool installer", file.FileDescription); Assert.Equal("x64", file.Architecture); Assert.Equal(MetadataSource.MsixManifest, file.MetadataSource); Assert.Equal(MetadataStatus.Success, file.MetadataStatus); Assert.Equal("metadata note", file.MetadataError); Assert.Equal("2.0", file.NormalizedVersion); Assert.Equal("PRODUCT-CODE", file.ProductCode); Assert.Equal("UPGRADE-CODE", file.UpgradeCode); Assert.Equal("tool.msix", file.PackageList); Assert.Equal(productId, file.ProductId); Assert.Equal(ProductMatchSource.MsixIdentity, file.ProductMatchSource); Assert.Equal(ProductMatchConfidence.High, file.ProductMatchConfidence); Assert.Equal("Contoso.Tool", file.MsixIdentityName);
        var product = Assert.Single(await database.GetProductsAsync(CancellationToken.None)); Assert.Equal(productId, product.Id); Assert.Equal("Tool", product.CanonicalName); Assert.Equal("Vendor", product.Publisher); Assert.Equal("tool", product.NormalizedName); Assert.Equal(DateTimeOffset.Parse(created), product.CreatedUtc); Assert.Equal(DateTimeOffset.Parse(updated), product.UpdatedUtc); Assert.Equal("1.0", product.LatestLocalVersion); Assert.Equal("3.0", product.LatestNormalizedVersion); Assert.Equal(UpdateStatus.UpdateAvailable, product.UpdateStatus); Assert.Equal("3.0", product.LatestVersion); Assert.Equal("WinGet", product.UpdateProvider); Assert.Equal("Vendor.Tool", product.ExternalProductId); Assert.Equal(DateTimeOffset.Parse(updated), product.LastCheckedUtc); Assert.Equal("remote note", product.UpdateError);
        var source = Assert.Single(await database.GetUpdateSourcesAsync(productId, CancellationToken.None)); Assert.Equal(sourceId, source.Id); Assert.Equal(productId, source.ProductId); Assert.Equal("WinGet", source.ProviderType); Assert.Equal("Vendor.Tool", source.ExternalId); Assert.True(source.Enabled); Assert.True(source.IsExplicit);
    }

    private async Task CreateFullV5Async(Guid productId, Guid sourceId, string created, string updated)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False"); await connection.OpenAsync(); await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys=ON;
            CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL);
            INSERT INTO schema_migrations VALUES(1,'2026-01-01T00:00:00.0000000+00:00'),(2,'2026-01-01T00:00:00.0000000+00:00'),(3,'2026-01-01T00:00:00.0000000+00:00'),(4,'2026-01-01T00:00:00.0000000+00:00'),(5,'2026-01-01T00:00:00.0000000+00:00');
            CREATE TABLE scan_roots (id INTEGER PRIMARY KEY, stored_path TEXT NOT NULL, path_kind INTEGER NOT NULL, include_subdirectories INTEGER NOT NULL, enabled INTEGER NOT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL);
            CREATE TABLE software_products (id TEXT PRIMARY KEY, canonical_name TEXT NOT NULL, publisher TEXT, normalized_name TEXT NOT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, latest_local_version TEXT, latest_normalized_version TEXT, update_status INTEGER NOT NULL DEFAULT 0, latest_version TEXT, update_provider TEXT, external_product_id TEXT, last_checked_utc TEXT, update_error TEXT);
            CREATE UNIQUE INDEX ix_software_products_name_publisher ON software_products(normalized_name, publisher);
            CREATE TABLE product_update_sources (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, provider_type TEXT NOT NULL, external_id TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1, is_explicit INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(product_id) REFERENCES software_products(id) ON DELETE CASCADE, UNIQUE(product_id, provider_type));
            CREATE TABLE installer_files (id INTEGER PRIMARY KEY, scan_root_id INTEGER NOT NULL, relative_path TEXT NOT NULL COLLATE NOCASE, file_name TEXT NOT NULL, extension TEXT NOT NULL, size INTEGER NOT NULL, last_write_utc TEXT NOT NULL, sha256 TEXT, first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL, exists_flag INTEGER NOT NULL, installer_kind INTEGER NOT NULL DEFAULT 0, product_name TEXT, product_version TEXT, publisher TEXT, file_version TEXT, file_description TEXT, architecture TEXT, metadata_source INTEGER NOT NULL DEFAULT 0, metadata_status INTEGER NOT NULL DEFAULT 0, metadata_error TEXT, normalized_version TEXT, product_code TEXT, upgrade_code TEXT, package_list TEXT, product_id TEXT REFERENCES software_products(id) ON DELETE SET NULL, product_match_source INTEGER, product_match_confidence INTEGER, msix_identity_name TEXT, FOREIGN KEY(scan_root_id) REFERENCES scan_roots(id) ON DELETE CASCADE, UNIQUE(scan_root_id, relative_path));
            CREATE INDEX ix_installer_files_scan_root ON installer_files(scan_root_id); CREATE INDEX ix_installer_files_last_seen ON installer_files(last_seen_utc); CREATE INDEX ix_installer_files_exists ON installer_files(exists_flag); CREATE INDEX ix_installer_files_product ON installer_files(product_id);
            INSERT INTO scan_roots VALUES(7,'C:\Catalog',0,1,1,$created,$updated);
            INSERT INTO software_products VALUES($product,'Tool','Vendor','tool',$created,$updated,'1.0','3.0',3,'3.0','WinGet','Vendor.Tool',$updated,'remote note');
            INSERT INTO product_update_sources VALUES($source,$product,'WinGet','Vendor.Tool',1,1);
            INSERT INTO installer_files VALUES(42,7,'nested\Tool.msix','Tool.msix','.msix',987654,$created,'ABCDEF',$created,$updated,1,3,'Tool','2.0','Vendor','2.0.0.0','Tool installer','x64',3,1,'metadata note','2.0','PRODUCT-CODE','UPGRADE-CODE','tool.msix',$product,2,0,'Contoso.Tool');
            """;
        command.Parameters.AddWithValue("$product", productId.ToString()); command.Parameters.AddWithValue("$source", sourceId.ToString()); command.Parameters.AddWithValue("$created", created); command.Parameters.AddWithValue("$updated", updated); await command.ExecuteNonQueryAsync();
    }
}

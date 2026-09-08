using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;

public sealed class Migration009InstalledSoftwareTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "catalog-m9-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task PersistsInventoryBindingAndMarksMissingEntries()
    {
        Directory.CreateDirectory(_folder); var database = new CatalogDatabase(Path.Combine(_folder,"catalog.db")); await database.InitializeAsync(CancellationToken.None); var now=DateTimeOffset.UtcNow; var item=new InstalledSoftware(0,"Contoso Tool","1.0","Contoso","contosotool","1.0",@"C:\Tool",null,"x64",InstalledSoftwareSource.Hklm64Uninstall,"Contoso.Tool",null,null,null,now,now,true);
        await database.UpsertInstalledSoftwareAsync([item],now,CancellationToken.None); var stored=Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None)); Assert.True(stored.Exists); Assert.Equal("Contoso Tool",stored.DisplayName);
        var product=new SoftwareProduct(Guid.NewGuid(),"Contoso Tool","Contoso","contosotool",now,now); await database.UpsertProductAsync(product,CancellationToken.None); await database.SetInstalledSoftwareBindingAsync(stored.Id,product.Id,InstalledSoftwareMatchSource.Manual,InstalledSoftwareMatchConfidence.Exact,CancellationToken.None);
        stored=Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None)); Assert.Equal(product.Id,stored.ProductId); Assert.Equal(InstalledSoftwareMatchSource.Manual,stored.MatchSource);
        await database.UpsertInstalledSoftwareAsync([],now.AddMinutes(1),CancellationToken.None); Assert.False(Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None)).Exists);
        await database.InitializeAsync(CancellationToken.None); Assert.Single(await database.GetInstalledSoftwareAsync(CancellationToken.None));
        await using var connection=new SqliteConnection($"Data Source={Path.Combine(_folder,"catalog.db")};Pooling=False"); await connection.OpenAsync(); await using var command=connection.CreateCommand(); command.CommandText="SELECT COUNT(*) FROM schema_migrations WHERE version=9"; Assert.Equal(1L,Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
    [Fact]
    public async Task ManualVersionEightDatabaseUpgradesToNineWithoutLosingExistingRows()
    {
        Directory.CreateDirectory(_folder); var path=Path.Combine(_folder,"v8.db"); var now="2026-01-01T00:00:00.0000000+00:00";
        await using(var connection=new SqliteConnection($"Data Source={path};Pooling=False")){await connection.OpenAsync();await using var command=connection.CreateCommand();command.CommandText=$"""CREATE TABLE schema_migrations(version INTEGER PRIMARY KEY,applied_utc TEXT NOT NULL); INSERT INTO schema_migrations SELECT value,'{now}' FROM json_each('[1,2,3,4,5,6,7,8]'); CREATE TABLE software_products(id TEXT PRIMARY KEY,canonical_name TEXT NOT NULL,publisher TEXT,normalized_name TEXT NOT NULL,created_utc TEXT NOT NULL,updated_utc TEXT NOT NULL,latest_local_version TEXT,latest_normalized_version TEXT,update_status INTEGER NOT NULL DEFAULT 0,latest_version TEXT,update_provider TEXT,external_product_id TEXT,last_checked_utc TEXT,update_error TEXT); CREATE TABLE scan_roots(id INTEGER PRIMARY KEY,stored_path TEXT NOT NULL); CREATE TABLE installer_files(id INTEGER PRIMARY KEY,file_name TEXT NOT NULL); INSERT INTO software_products VALUES('11111111-1111-1111-1111-111111111111','Tool','Vendor','tool','{now}','{now}',NULL,NULL,0,NULL,NULL,NULL,NULL,NULL); INSERT INTO scan_roots VALUES(1,'C:\\Stage1'); INSERT INTO installer_files VALUES(1,'Stage3.msi');""";await command.ExecuteNonQueryAsync();}
        var database=new CatalogDatabase(path);await database.InitializeAsync(CancellationToken.None);await database.InitializeAsync(CancellationToken.None);await using var verify=new SqliteConnection($"Data Source={path};Pooling=False");await verify.OpenAsync();await using var check=verify.CreateCommand();check.CommandText="SELECT group_concat(version, ',') FROM (SELECT version FROM schema_migrations ORDER BY version)";Assert.Equal("1,2,3,4,5,6,7,8,9",await check.ExecuteScalarAsync());check.CommandText="SELECT canonical_name FROM software_products";Assert.Equal("Tool",await check.ExecuteScalarAsync());check.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='installed_software'";Assert.Equal(1L,Convert.ToInt64(await check.ExecuteScalarAsync()));check.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='ix_installed_software_product'";Assert.Equal(1L,Convert.ToInt64(await check.ExecuteScalarAsync()));
    }
    public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
}

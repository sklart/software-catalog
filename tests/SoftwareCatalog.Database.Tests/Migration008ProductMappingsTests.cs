using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;

public sealed class Migration008ProductMappingsTests : IDisposable
{
    private readonly string _folder=Path.Combine(Path.GetTempPath(),"catalog-m8-"+Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ManualV7DatabaseUpgradesLegacyMappingsAndIsIdempotent()
    {
        Directory.CreateDirectory(_folder); var path=Path.Combine(_folder,"catalog.db"); var now="2026-01-01T00:00:00.0000000+00:00";
        await using(var c=new SqliteConnection($"Data Source={path};Pooling=False")) { await c.OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText=$"""
CREATE TABLE schema_migrations(version INTEGER PRIMARY KEY,applied_utc TEXT NOT NULL); INSERT INTO schema_migrations VALUES(1,'{now}'),(2,'{now}'),(3,'{now}'),(4,'{now}'),(5,'{now}'),(6,'{now}'),(7,'{now}');
CREATE TABLE software_products(id TEXT PRIMARY KEY,canonical_name TEXT NOT NULL,publisher TEXT,normalized_name TEXT NOT NULL,created_utc TEXT NOT NULL,updated_utc TEXT NOT NULL,latest_local_version TEXT,latest_normalized_version TEXT,update_status INTEGER NOT NULL DEFAULT 0,latest_version TEXT,update_provider TEXT,external_product_id TEXT,last_checked_utc TEXT,update_error TEXT);
CREATE TABLE product_update_sources(id TEXT PRIMARY KEY,product_id TEXT NOT NULL,provider_type TEXT NOT NULL,external_id TEXT NOT NULL,enabled INTEGER NOT NULL DEFAULT 1,is_explicit INTEGER NOT NULL DEFAULT 0,FOREIGN KEY(product_id) REFERENCES software_products(id) ON DELETE CASCADE,UNIQUE(product_id,provider_type));
INSERT INTO software_products VALUES('11111111-1111-1111-1111-111111111111','Tool','Vendor','tool','{now}','{now}','1.0','1.0',0,NULL,NULL,NULL,NULL,NULL);
INSERT INTO product_update_sources VALUES('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','WinGet','Vendor.Tool',1,0);
"""; await cmd.ExecuteNonQueryAsync(); }
        var database=new CatalogDatabase(path); await database.InitializeAsync(CancellationToken.None); await database.InitializeAsync(CancellationToken.None); var productId=Guid.Parse("11111111-1111-1111-1111-111111111111"); var mapping=Assert.Single(await database.GetUpdateSourcesAsync(productId,CancellationToken.None));
        Assert.Equal(MappingSource.ExactMatch,mapping.Source); Assert.Equal(MappingConfidence.High,mapping.Confidence); Assert.NotNull(mapping.CreatedUtc); Assert.NotNull(mapping.UpdatedUtc); Assert.True(mapping.Enabled); Assert.False(mapping.IsExplicit);
        await using var verify=new SqliteConnection($"Data Source={path};Pooling=False"); await verify.OpenAsync(); await using var command=verify.CreateCommand(); command.CommandText="SELECT group_concat(version, ',') FROM (SELECT version FROM schema_migrations ORDER BY version)"; Assert.Equal("1,2,3,4,5,6,7,8",await command.ExecuteScalarAsync()); command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('product_aliases','update_candidate_cache')"; Assert.Equal(2L,Convert.ToInt64(await command.ExecuteScalarAsync()));
    }
    public void Dispose(){if(Directory.Exists(_folder))Directory.Delete(_folder,true);}
}

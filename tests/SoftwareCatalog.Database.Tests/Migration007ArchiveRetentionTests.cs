using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;

namespace SoftwareCatalog.Database.Tests;

public sealed class Migration007ArchiveRetentionTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "catalog-m7-" + Guid.NewGuid().ToString("N"));
    [Fact] public async Task RealV6FixtureUpgradesIdempotentlyAndPreservesExistingState()
    {
        Directory.CreateDirectory(_folder); var path=Path.Combine(_folder,"catalog.db"); var initial=new CatalogDatabase(path); await initial.InitializeAsync(CancellationToken.None);
        var root=await initial.AddScanRootAsync("C:\\Installers",ScanRootPathKind.Absolute,true,CancellationToken.None); var now=DateTimeOffset.UtcNow; var file=new InstallerFile(0,root.Id,"tool.exe","tool.exe",".exe",123,now,"ABC",now,now,true,ProductName:"Tool",ProductVersion:"2.0",NormalizedVersion:"2.0"); await initial.UpsertInstallersAsync([file],CancellationToken.None); var stored=Assert.Single(await initial.GetInstallersAsync(CancellationToken.None));
        await using (var c=new SqliteConnection($"Data Source={path};Pooling=False")) { await c.OpenAsync(); await using var cmd=c.CreateCommand(); cmd.CommandText="DROP TABLE archive_operations; DROP INDEX ix_installer_files_storage_state; ALTER TABLE installer_files DROP COLUMN storage_state; ALTER TABLE installer_files DROP COLUMN is_pinned; ALTER TABLE installer_files DROP COLUMN storage_changed_utc; ALTER TABLE installer_files DROP COLUMN original_scan_root_id; ALTER TABLE installer_files DROP COLUMN original_relative_path; ALTER TABLE scan_roots DROP COLUMN role; DELETE FROM schema_migrations WHERE version=7;"; await cmd.ExecuteNonQueryAsync(); }
        var upgraded=new CatalogDatabase(path); await upgraded.InitializeAsync(CancellationToken.None); await upgraded.InitializeAsync(CancellationToken.None);
        var result=Assert.Single(await upgraded.GetInstallersAsync(CancellationToken.None)); Assert.Equal(stored.Id,result.Id); Assert.Equal("ABC",result.Sha256); Assert.Equal(InstallerStorageState.Active,result.StorageState); Assert.False(result.IsPinned); Assert.Null(result.StorageChangedUtc);
        await using var verify=new SqliteConnection($"Data Source={path};Pooling=False"); await verify.OpenAsync(); await using var versions=verify.CreateCommand(); versions.CommandText="SELECT group_concat(version, ',') FROM (SELECT version FROM schema_migrations ORDER BY version)"; Assert.Equal("1,2,3,4,5,6,7",await versions.ExecuteScalarAsync()); versions.CommandText="SELECT COUNT(*) FROM archive_operations"; Assert.Equal(0L,Convert.ToInt64(await versions.ExecuteScalarAsync()));
    }
    public void Dispose() { if(Directory.Exists(_folder)) Directory.Delete(_folder,true); }
}

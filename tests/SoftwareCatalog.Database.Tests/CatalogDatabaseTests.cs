using SoftwareCatalog.Core.Domain;
using SoftwareCatalog.Database;
using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Tests;
public sealed class CatalogDatabaseTests : IAsyncLifetime
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private CatalogDatabase _database = null!;
    public async Task InitializeAsync() { Directory.CreateDirectory(_folder); _database = new CatalogDatabase(Path.Combine(_folder, "catalog.db")); await _database.InitializeAsync(CancellationToken.None); }
    public Task DisposeAsync() { Directory.Delete(_folder, true); return Task.CompletedTask; }
    [Fact] public async Task PersistsRootAndInstallerAndMarksMissing()
    {
        var root = await _database.AddScanRootAsync("..\\Software", ScanRootPathKind.RelativeToApplication, true, CancellationToken.None);
        var now = DateTimeOffset.UtcNow; await _database.UpsertInstallersAsync([new(0, root.Id, "tool.exe", "tool.exe", ".exe", 1, now, "A", now, now, true)], CancellationToken.None);
        Assert.Single(await _database.GetInstallersAsync(CancellationToken.None)); await _database.MarkMissingAsync(root.Id, now.AddSeconds(1), CancellationToken.None);
        Assert.False((await _database.GetInstallersAsync(CancellationToken.None)).Single().Exists);
    }
    [Fact] public async Task CaseOnlyPathIsUpdatedInsteadOfDuplicated()
    {
        var root = await _database.AddScanRootAsync("C:\\Software", ScanRootPathKind.Absolute, true, CancellationToken.None); var now = DateTimeOffset.UtcNow;
        await _database.UpsertInstallersAsync([new(0, root.Id, "Tool.EXE", "Tool.EXE", ".exe", 1, now, "A", now, now, true)], CancellationToken.None);
        await _database.UpsertInstallersAsync([new(0, root.Id, "tool.exe", "tool.exe", ".exe", 2, now, "B", now, now, true)], CancellationToken.None);
        var file = (await _database.GetInstallersAsync(CancellationToken.None)).Single(); Assert.Equal(2, file.Size); Assert.Equal("B", file.Sha256);
    }
    [Fact] public async Task MigrationAndConstraintsAreEnforced()
    {
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(_folder, "catalog.db")};Pooling=False"); await connection.OpenAsync();
        await using (var pragma = connection.CreateCommand()) { pragma.CommandText = "PRAGMA foreign_keys=ON"; await pragma.ExecuteNonQueryAsync(); }
        await using (var migration = connection.CreateCommand()) { migration.CommandText = "SELECT MAX(version) FROM schema_migrations"; Assert.Equal(2L, Convert.ToInt64(await migration.ExecuteScalarAsync())); }
        await using (var foreignKey = connection.CreateCommand()) { foreignKey.CommandText = "INSERT INTO installer_files(scan_root_id,relative_path,file_name,extension,size,last_write_utc,first_seen_utc,last_seen_utc,exists_flag) VALUES(999,'orphan.exe','orphan.exe','.exe',1,'2026-01-01','2026-01-01','2026-01-01',1)"; await Assert.ThrowsAsync<SqliteException>(() => foreignKey.ExecuteNonQueryAsync()); }
        var root = await _database.AddScanRootAsync("C:\\Unique", ScanRootPathKind.Absolute, true, CancellationToken.None); var now = DateTimeOffset.UtcNow;
        await _database.UpsertInstallersAsync([new(0, root.Id, "duplicate.exe", "duplicate.exe", ".exe", 1, now, null, now, now, true)], CancellationToken.None);
        await _database.UpsertInstallersAsync([new(0, root.Id, "DUPLICATE.EXE", "DUPLICATE.EXE", ".exe", 2, now, null, now, now, true)], CancellationToken.None);
        Assert.Single(await _database.GetInstallersAsync(CancellationToken.None), file => file.ScanRootId == root.Id);
    }
    [Fact] public async Task DirectCaseOnlyInsertViolatesUniqueConstraint()
    {
        var root = await _database.AddScanRootAsync("C:\\Direct", ScanRootPathKind.Absolute, true, CancellationToken.None); await using var connection = new SqliteConnection($"Data Source={Path.Combine(_folder, "catalog.db")};Pooling=False"); await connection.OpenAsync();
        await using (var first = connection.CreateCommand()) { first.CommandText = $"INSERT INTO installer_files(scan_root_id,relative_path,file_name,extension,size,last_write_utc,first_seen_utc,last_seen_utc,exists_flag) VALUES({root.Id},'Tool.exe','Tool.exe','.exe',1,'2026','2026','2026',1)"; await first.ExecuteNonQueryAsync(); }
        await using var second = connection.CreateCommand(); second.CommandText = $"INSERT INTO installer_files(scan_root_id,relative_path,file_name,extension,size,last_write_utc,first_seen_utc,last_seen_utc,exists_flag) VALUES({root.Id},'tool.EXE','tool.EXE','.exe',1,'2026','2026','2026',1)"; await Assert.ThrowsAsync<SqliteException>(() => second.ExecuteNonQueryAsync());
    }
    [Fact] public async Task InitializerUpgradesRealVersionOneDatabaseToVersionTwo()
    {
        var path = Path.Combine(_folder, "v1.db"); const string timestamp = "2026-01-01T00:00:00.0000000+00:00";
        await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False")) { await connection.OpenAsync(); await using var command = connection.CreateCommand(); command.CommandText = """CREATE TABLE schema_migrations (version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL); INSERT INTO schema_migrations VALUES(1,'2026-01-01T00:00:00.0000000+00:00'); CREATE TABLE scan_roots (id INTEGER PRIMARY KEY, stored_path TEXT NOT NULL, path_kind INTEGER NOT NULL, include_subdirectories INTEGER NOT NULL, enabled INTEGER NOT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL); CREATE TABLE installer_files (id INTEGER PRIMARY KEY, scan_root_id INTEGER NOT NULL, relative_path TEXT NOT NULL, file_name TEXT NOT NULL, extension TEXT NOT NULL, size INTEGER NOT NULL, last_write_utc TEXT NOT NULL, sha256 TEXT, first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL, exists_flag INTEGER NOT NULL, FOREIGN KEY(scan_root_id) REFERENCES scan_roots(id) ON DELETE CASCADE, UNIQUE(scan_root_id, relative_path)); CREATE INDEX ix_installer_files_scan_root ON installer_files(scan_root_id); CREATE INDEX ix_installer_files_last_seen ON installer_files(last_seen_utc); CREATE INDEX ix_installer_files_exists ON installer_files(exists_flag);"""; await command.ExecuteNonQueryAsync(); command.CommandText = "INSERT INTO scan_roots VALUES(1,'C:\\Software',0,1,1,$timestamp,$timestamp); INSERT INTO installer_files VALUES(1,1,'Tool.EXE','Tool.EXE','.exe',1,$timestamp,NULL,$timestamp,$timestamp,1);"; command.Parameters.AddWithValue("$timestamp", timestamp); await command.ExecuteNonQueryAsync(); }
        var upgraded = new CatalogDatabase(path); await upgraded.InitializeAsync(CancellationToken.None); var files = await upgraded.GetInstallersAsync(CancellationToken.None); Assert.Single(files); await upgraded.UpsertInstallersAsync([files.Single() with { RelativePath = "tool.exe", Size = 2 }], CancellationToken.None); Assert.Single(await upgraded.GetInstallersAsync(CancellationToken.None));
    }
}

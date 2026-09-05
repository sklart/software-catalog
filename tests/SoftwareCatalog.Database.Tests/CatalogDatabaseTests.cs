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
}

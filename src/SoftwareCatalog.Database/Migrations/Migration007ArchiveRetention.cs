using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Migrations;

internal sealed class Migration007ArchiveRetention : IMigration
{
    public int Version => 7;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        if (await ExistsAsync("installer_files"))
        {
            await ExecuteAsync("ALTER TABLE installer_files ADD COLUMN storage_state INTEGER NOT NULL DEFAULT 0; ALTER TABLE installer_files ADD COLUMN is_pinned INTEGER NOT NULL DEFAULT 0; ALTER TABLE installer_files ADD COLUMN storage_changed_utc TEXT; ALTER TABLE installer_files ADD COLUMN original_scan_root_id INTEGER; ALTER TABLE installer_files ADD COLUMN original_relative_path TEXT; CREATE INDEX ix_installer_files_storage_state ON installer_files(storage_state);");
        }
        if (await ExistsAsync("scan_roots")) await ExecuteAsync("ALTER TABLE scan_roots ADD COLUMN role INTEGER NOT NULL DEFAULT 0;");
        await ExecuteAsync("CREATE TABLE archive_operations (id TEXT PRIMARY KEY, installer_id INTEGER NOT NULL, product_id TEXT, operation_type INTEGER NOT NULL, from_state INTEGER NOT NULL, to_state INTEGER, source_path TEXT NOT NULL, destination_path TEXT, sha256 TEXT, status INTEGER NOT NULL, error TEXT, started_utc TEXT NOT NULL, completed_utc TEXT); CREATE INDEX ix_archive_operations_installer ON archive_operations(installer_id, started_utc DESC);");

        async Task<bool> ExistsAsync(string name) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name"; command.Parameters.AddWithValue("$name", name); return Convert.ToInt64(await command.ExecuteScalarAsync(token)) != 0; }
        async Task ExecuteAsync(string sql) { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; await command.ExecuteNonQueryAsync(token); }
    }
}

using Microsoft.Data.Sqlite;
namespace SoftwareCatalog.Database.Migrations;
internal sealed class Migration001Initial : IMigration
{
    public int Version => 1;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = """
CREATE TABLE scan_roots (id INTEGER PRIMARY KEY, stored_path TEXT NOT NULL, path_kind INTEGER NOT NULL, include_subdirectories INTEGER NOT NULL, enabled INTEGER NOT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL);
CREATE TABLE installer_files (id INTEGER PRIMARY KEY, scan_root_id INTEGER NOT NULL, relative_path TEXT NOT NULL COLLATE NOCASE, file_name TEXT NOT NULL, extension TEXT NOT NULL, size INTEGER NOT NULL, last_write_utc TEXT NOT NULL, sha256 TEXT, first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL, exists_flag INTEGER NOT NULL, FOREIGN KEY(scan_root_id) REFERENCES scan_roots(id) ON DELETE CASCADE, UNIQUE(scan_root_id, relative_path));
CREATE INDEX ix_installer_files_scan_root ON installer_files(scan_root_id); CREATE INDEX ix_installer_files_last_seen ON installer_files(last_seen_utc); CREATE INDEX ix_installer_files_exists ON installer_files(exists_flag);
"""; await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

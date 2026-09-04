using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Migrations;

internal sealed class Migration002CaseInsensitivePaths : IMigration
{
    public int Version => 2;

    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DROP INDEX ix_installer_files_scan_root;
            DROP INDEX ix_installer_files_last_seen;
            DROP INDEX ix_installer_files_exists;
            ALTER TABLE installer_files RENAME TO installer_files_v1;
            CREATE TABLE installer_files (
                id INTEGER PRIMARY KEY,
                scan_root_id INTEGER NOT NULL,
                relative_path TEXT NOT NULL COLLATE NOCASE,
                file_name TEXT NOT NULL,
                extension TEXT NOT NULL,
                size INTEGER NOT NULL,
                last_write_utc TEXT NOT NULL,
                sha256 TEXT,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL,
                exists_flag INTEGER NOT NULL,
                FOREIGN KEY(scan_root_id) REFERENCES scan_roots(id) ON DELETE CASCADE,
                UNIQUE(scan_root_id, relative_path));
            INSERT INTO installer_files SELECT * FROM installer_files_v1;
            DROP TABLE installer_files_v1;
            CREATE INDEX ix_installer_files_scan_root ON installer_files(scan_root_id);
            CREATE INDEX ix_installer_files_last_seen ON installer_files(last_seen_utc);
            CREATE INDEX ix_installer_files_exists ON installer_files(exists_flag);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Migrations;

internal sealed class Migration003InstallerMetadata : IMigration
{
    public int Version => 3;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE installer_files ADD COLUMN installer_kind INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE installer_files ADD COLUMN product_name TEXT;
            ALTER TABLE installer_files ADD COLUMN product_version TEXT;
            ALTER TABLE installer_files ADD COLUMN publisher TEXT;
            ALTER TABLE installer_files ADD COLUMN file_version TEXT;
            ALTER TABLE installer_files ADD COLUMN file_description TEXT;
            ALTER TABLE installer_files ADD COLUMN architecture TEXT;
            ALTER TABLE installer_files ADD COLUMN metadata_source INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE installer_files ADD COLUMN metadata_status INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE installer_files ADD COLUMN metadata_error TEXT;
            ALTER TABLE installer_files ADD COLUMN normalized_version TEXT;
            ALTER TABLE installer_files ADD COLUMN product_code TEXT;
            ALTER TABLE installer_files ADD COLUMN upgrade_code TEXT;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

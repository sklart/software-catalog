using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Migrations;

internal sealed class Migration005ProductsAndUpdates : IMigration
{
    public int Version => 5;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE software_products (id TEXT PRIMARY KEY, canonical_name TEXT NOT NULL, publisher TEXT, normalized_name TEXT NOT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, latest_local_version TEXT, latest_normalized_version TEXT, update_status INTEGER NOT NULL DEFAULT 0, latest_version TEXT, update_provider TEXT, external_product_id TEXT, last_checked_utc TEXT, update_error TEXT);
            CREATE UNIQUE INDEX ix_software_products_name_publisher ON software_products(normalized_name, publisher);
            CREATE TABLE product_update_sources (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, provider_type TEXT NOT NULL, external_id TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1, is_explicit INTEGER NOT NULL DEFAULT 0, FOREIGN KEY(product_id) REFERENCES software_products(id) ON DELETE CASCADE, UNIQUE(product_id, provider_type));
            ALTER TABLE installer_files ADD COLUMN product_id TEXT REFERENCES software_products(id) ON DELETE SET NULL;
            ALTER TABLE installer_files ADD COLUMN product_match_source INTEGER;
            ALTER TABLE installer_files ADD COLUMN product_match_confidence INTEGER;
            ALTER TABLE installer_files ADD COLUMN msix_identity_name TEXT;
            CREATE INDEX ix_installer_files_product ON installer_files(product_id);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

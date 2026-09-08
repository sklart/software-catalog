using Microsoft.Data.Sqlite;
namespace SoftwareCatalog.Database.Migrations;
internal sealed class Migration009InstalledSoftware : IMigration
{
    public int Version => 9;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS installed_software (id INTEGER PRIMARY KEY AUTOINCREMENT, display_name TEXT NOT NULL, display_version TEXT, publisher TEXT NOT NULL DEFAULT '', normalized_name TEXT NOT NULL, normalized_version TEXT NOT NULL DEFAULT '', install_location TEXT NOT NULL DEFAULT '', install_date_utc TEXT, architecture TEXT NOT NULL DEFAULT '', source INTEGER NOT NULL, external_id TEXT, product_id TEXT REFERENCES software_products(id) ON DELETE SET NULL, match_source INTEGER, match_confidence INTEGER, first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL, exists_flag INTEGER NOT NULL DEFAULT 1, UNIQUE(normalized_name,publisher,normalized_version,architecture,install_location));
            CREATE INDEX IF NOT EXISTS ix_installed_software_product ON installed_software(product_id);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

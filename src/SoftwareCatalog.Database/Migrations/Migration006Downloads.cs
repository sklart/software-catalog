using Microsoft.Data.Sqlite;
namespace SoftwareCatalog.Database.Migrations;
internal sealed class Migration006Downloads : IMigration
{
    public int Version => 6;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    { await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "CREATE TABLE download_history (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, provider_type TEXT NOT NULL, external_product_id TEXT NOT NULL, version TEXT, file_name TEXT, source_url TEXT, expected_sha256 TEXT, actual_sha256 TEXT, status INTEGER NOT NULL, error TEXT, started_utc TEXT NOT NULL, completed_utc TEXT, final_relative_path TEXT); CREATE INDEX ix_download_history_product ON download_history(product_id);"; await command.ExecuteNonQueryAsync(token); }
}

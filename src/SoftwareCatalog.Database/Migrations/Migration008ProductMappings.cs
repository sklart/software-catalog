using Microsoft.Data.Sqlite;
namespace SoftwareCatalog.Database.Migrations;
internal sealed class Migration008ProductMappings : IMigration
{
    public int Version => 8;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var info=connection.CreateCommand()) { info.Transaction=transaction; info.CommandText="PRAGMA table_info(product_update_sources)"; await using var reader=await info.ExecuteReaderAsync(cancellationToken); while(await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(1)); }
        await using var command=connection.CreateCommand(); command.Transaction=transaction;
        var changes = new List<string>();
        if (!columns.Contains("enabled")) changes.Add("ALTER TABLE product_update_sources ADD COLUMN enabled INTEGER NOT NULL DEFAULT 1");
        if (!columns.Contains("is_explicit")) changes.Add("ALTER TABLE product_update_sources ADD COLUMN is_explicit INTEGER NOT NULL DEFAULT 0");
        if (!columns.Contains("source")) changes.Add("ALTER TABLE product_update_sources ADD COLUMN source INTEGER NOT NULL DEFAULT 5");
        if (!columns.Contains("confidence")) changes.Add("ALTER TABLE product_update_sources ADD COLUMN confidence INTEGER NOT NULL DEFAULT 2");
        if (!columns.Contains("created_utc")) changes.Add("ALTER TABLE product_update_sources ADD COLUMN created_utc TEXT NOT NULL DEFAULT ''");
        if (!columns.Contains("updated_utc")) changes.Add("ALTER TABLE product_update_sources ADD COLUMN updated_utc TEXT NOT NULL DEFAULT ''");
        foreach(var change in changes) { command.CommandText=change; await command.ExecuteNonQueryAsync(cancellationToken); }
        command.CommandText="""
            UPDATE product_update_sources SET source=CASE WHEN is_explicit=1 THEN 0 ELSE 2 END, confidence=CASE WHEN is_explicit=1 THEN 0 ELSE 1 END, created_utc=CASE WHEN created_utc='' THEN strftime('%Y-%m-%dT%H:%M:%fZ','now') ELSE created_utc END, updated_utc=CASE WHEN updated_utc='' THEN strftime('%Y-%m-%dT%H:%M:%fZ','now') ELSE updated_utc END;
            CREATE TABLE IF NOT EXISTS product_aliases(product_id TEXT NOT NULL,alias TEXT NOT NULL,normalized_alias TEXT NOT NULL,source INTEGER NOT NULL DEFAULT 5,PRIMARY KEY(product_id,normalized_alias),FOREIGN KEY(product_id) REFERENCES software_products(id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS update_candidate_cache(product_id TEXT NOT NULL,provider_type TEXT NOT NULL,external_id TEXT NOT NULL,display_name TEXT NOT NULL,publisher_or_owner TEXT,latest_version TEXT,confidence INTEGER NOT NULL,reason TEXT NOT NULL,resolved_utc TEXT NOT NULL,PRIMARY KEY(product_id,provider_type,external_id));
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

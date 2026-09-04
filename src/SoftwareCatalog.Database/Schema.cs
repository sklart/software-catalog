namespace SoftwareCatalog.Database;

internal static class Schema
{
    internal const string Sql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_utc TEXT NOT NULL);
        INSERT OR IGNORE INTO schema_migrations(version, applied_utc) VALUES (1, CURRENT_TIMESTAMP);
        CREATE TABLE IF NOT EXISTS software_products (id TEXT PRIMARY KEY, name TEXT NOT NULL, publisher TEXT);
        CREATE TABLE IF NOT EXISTS software_aliases (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, alias TEXT NOT NULL, is_confirmed INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS installer_files (id INTEGER PRIMARY KEY, full_path TEXT NOT NULL UNIQUE, file_name TEXT NOT NULL, extension TEXT NOT NULL, size INTEGER NOT NULL, last_write_utc TEXT NOT NULL, sha256 TEXT, first_seen_utc TEXT NOT NULL, last_seen_utc TEXT NOT NULL, exists_flag INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS software_versions (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, version TEXT NOT NULL, channel TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS update_sources (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, provider_id TEXT NOT NULL, external_id TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS update_checks (id TEXT PRIMARY KEY, product_id TEXT NOT NULL, status TEXT NOT NULL, checked_utc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS match_candidates (id TEXT PRIMARY KEY, installer_file_id INTEGER NOT NULL, product_id TEXT NOT NULL, confidence INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
        """;
}

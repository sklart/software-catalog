namespace SoftwareCatalog.Database.Migrations;
internal interface IMigration { int Version { get; } Task ApplyAsync(Microsoft.Data.Sqlite.SqliteConnection connection, Microsoft.Data.Sqlite.SqliteTransaction transaction, CancellationToken cancellationToken); }

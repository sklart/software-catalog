using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Abstractions;

namespace SoftwareCatalog.Database.Migrations;

internal sealed class MigrationRunner(IEnumerable<IMigration> migrations, IAppLogger? logger)
{
    public async Task ApplyAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        foreach (var migration in migrations)
        {
            await using var check = connection.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version=$version";
            check.Parameters.AddWithValue("$version", migration.Version);
            if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) != 0) continue;
            logger?.Information("migration", $"Starting version {migration.Version}.");
            try
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await migration.ApplyAsync(connection, (SqliteTransaction)transaction, cancellationToken);
                await using var mark = connection.CreateCommand(); mark.Transaction = (SqliteTransaction)transaction;
                mark.CommandText = "INSERT INTO schema_migrations(version, applied_utc) VALUES($version,$utc)";
                mark.Parameters.AddWithValue("$version", migration.Version); mark.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
                await mark.ExecuteNonQueryAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
                logger?.Information("migration", $"Applied version {migration.Version}.");
            }
            catch (Exception exception)
            {
                logger?.Error("migration", $"Version {migration.Version}: {exception.Message}");
                throw;
            }
        }
    }
}

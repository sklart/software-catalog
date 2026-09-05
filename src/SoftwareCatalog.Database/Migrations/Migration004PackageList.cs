using Microsoft.Data.Sqlite;

namespace SoftwareCatalog.Database.Migrations;

internal sealed class Migration004PackageList : IMigration
{
    public int Version => 4;
    public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "ALTER TABLE installer_files ADD COLUMN package_list TEXT;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

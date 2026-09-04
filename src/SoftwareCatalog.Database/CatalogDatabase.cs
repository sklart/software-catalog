using Microsoft.Data.Sqlite;
using SoftwareCatalog.Core.Abstractions;
using SoftwareCatalog.Core.Domain;

namespace SoftwareCatalog.Database;

public sealed class CatalogDatabase(string databasePath) : IInstallerFileRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(databasePath); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = Schema.Sql; await command.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task<InstallerFile?> FindByPathAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT full_path,file_name,extension,size,last_write_utc,sha256,first_seen_utc,last_seen_utc,exists_flag FROM installer_files WHERE full_path=$path"; command.Parameters.AddWithValue("$path", fullPath);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), DateTimeOffset.Parse(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetString(5), DateTimeOffset.Parse(reader.GetString(6)), DateTimeOffset.Parse(reader.GetString(7)), reader.GetBoolean(8));
    }
    public async Task UpsertAsync(InstallerFile file, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_connectionString); await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """INSERT INTO installer_files(full_path,file_name,extension,size,last_write_utc,sha256,first_seen_utc,last_seen_utc,exists_flag) VALUES($path,$name,$extension,$size,$modified,$hash,$firstSeen,$lastSeen,$exists) ON CONFLICT(full_path) DO UPDATE SET file_name=excluded.file_name,extension=excluded.extension,size=excluded.size,last_write_utc=excluded.last_write_utc,sha256=excluded.sha256,last_seen_utc=excluded.last_seen_utc,exists_flag=excluded.exists_flag;""";
        command.Parameters.AddWithValue("$path", file.FullPath); command.Parameters.AddWithValue("$name", file.FileName); command.Parameters.AddWithValue("$extension", file.Extension); command.Parameters.AddWithValue("$size", file.Size); command.Parameters.AddWithValue("$modified", file.LastWriteTimeUtc.ToString("O")); command.Parameters.AddWithValue("$hash", (object?)file.Sha256 ?? DBNull.Value); command.Parameters.AddWithValue("$firstSeen", file.FirstSeenUtc.ToString("O")); command.Parameters.AddWithValue("$lastSeen", file.LastSeenUtc.ToString("O")); command.Parameters.AddWithValue("$exists", file.Exists);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

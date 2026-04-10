using System.IO;
using Microsoft.Data.Sqlite;

namespace SSOInventoryManager.Services;

public class DatabaseService : IDisposable
{
    private readonly SqliteConnection _connection;

    public DatabaseService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SSOInventoryManager");
        Directory.CreateDirectory(dir);
        var dbPath = Path.Combine(dir, "appdata.db");

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        Initialize();
    }

    private void Initialize()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AppNotes (
                AppKey TEXT PRIMARY KEY,
                Source TEXT NOT NULL,
                Notes TEXT NOT NULL DEFAULT '',
                FlaggedForReview INTEGER NOT NULL DEFAULT 0
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public (string Notes, bool Flagged) GetAppData(string appKey, string source)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Notes, FlaggedForReview FROM AppNotes WHERE AppKey = @key AND Source = @source";
        cmd.Parameters.AddWithValue("@key", appKey);
        cmd.Parameters.AddWithValue("@source", source);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return (reader.GetString(0), reader.GetInt32(1) == 1);
        }
        return (string.Empty, false);
    }

    public void SaveAppData(string appKey, string source, string notes, bool flagged)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AppNotes (AppKey, Source, Notes, FlaggedForReview)
            VALUES (@key, @source, @notes, @flagged)
            ON CONFLICT(AppKey) DO UPDATE SET
                Notes = @notes,
                FlaggedForReview = @flagged
            """;
        cmd.Parameters.AddWithValue("@key", appKey);
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@notes", notes);
        cmd.Parameters.AddWithValue("@flagged", flagged ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}

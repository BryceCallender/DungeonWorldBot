using DungeonWorldBot.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Tests;

/// <summary>
/// A fresh in-memory SQLite database with every migration applied, so tests run against the same schema the bot gets.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    // Migrating takes a while, so it's done once and each database starts as a copy of this one
    private static readonly Lazy<SqliteConnection> Migrated = new(() =>
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        context.Database.Migrate();
        return connection;
    });

    private static readonly Lock CopyLock = new();

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        lock (CopyLock)
            Migrated.Value.BackupDatabase(_connection);
    }

    public DungeonWorldContext CreateContext() => CreateContext(_connection);

    /// <summary>
    /// Saves this database to a file, for what opens the database by its path rather than through a context.
    /// </summary>
    public void CopyTo(string path)
    {
        using var file = new SqliteConnection($"Data Source={path};Pooling=False");
        file.Open();
        _connection.BackupDatabase(file);
    }

    public void Dispose() => _connection.Dispose();

    private static DungeonWorldContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(connection).Options);
}

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

    public void Dispose() => _connection.Dispose();

    private static DungeonWorldContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(connection).Options);
}

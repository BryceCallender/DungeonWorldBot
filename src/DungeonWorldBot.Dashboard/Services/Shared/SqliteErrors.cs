using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// Telling apart the errors from a database the bot hasn't migrated yet. The dashboard only reads, so it can't add what's
/// missing; the bot's migration does, and until then the pages show nothing rather than failing.
/// </summary>
public static class SqliteErrors
{
    /// <summary>
    /// Whether a table it asked for isn't there yet.
    /// </summary>
    public static bool IsMissingTable(this SqliteException e) => e.Message.Contains("no such table");

    /// <summary>
    /// Whether a column it asked for isn't there yet.
    /// </summary>
    public static bool IsMissingColumn(this SqliteException e) => e.Message.Contains("no such column");

    /// <summary>
    /// Whether a table or a column it asked for isn't there yet.
    /// </summary>
    public static bool IsMissingSchema(this SqliteException e) => e.IsMissingTable() || e.IsMissingColumn();
}

using DungeonWorldBot.Dashboard.Models.Admin;
using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// The bits of SQL the admin pages share. Table and column names aren't known until someone opens one, so they can't be
/// parameters: they go into the statement itself, quoted, and only ever names the database gave back. Values always go
/// in as parameters.
/// </summary>
public static class AdminSql
{
    public static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// A table's columns to select. Files are only measured, so a page of characters doesn't carry all their pictures.
    /// </summary>
    public static string SelectList(IReadOnlyList<AdminColumn> columns) =>
        string.Join(", ", columns.Select(c => c.IsBlob ? $"length({Quote(c.Name)})" : Quote(c.Name)));

    public static SqliteCommand Command(this SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    /// <summary>
    /// A value as read, with a file as its size.
    /// </summary>
    public static object? Value(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return null;
        return reader.GetValue(ordinal) is byte[] bytes ? new AdminBlob(bytes.Length) : reader.GetValue(ordinal);
    }

    public static string Explain(SqliteException e) => e.SqliteErrorCode switch
    {
        // SQLITE_CONSTRAINT: already taken, left empty, or pointing at a row that isn't there
        19 => $"The database said no: {e.Message.Replace("SQLite Error 19: ", "")}",
        _ => e.Message,
    };
}

using DungeonWorldBot.Dashboard.Models.Admin;
using Microsoft.Data.Sqlite;
using static DungeonWorldBot.Dashboard.Services.Admin.AdminSql;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// An admin changing anything at all in the databases, a row at a time. Unlike the other desks it goes around the bot's
/// rules, straight to the tables, so it can fix what they can't.
/// </summary>
/// <remarks>
/// Rows are found by SQLite's rowid, which every table the bot makes has.
/// </remarks>
public class AdminDesk(AdminSchema schema)
{
    public const int PageSize = 50;

    /// <param name="search">Text to find in any column, or null for every row.</param>
    /// <param name="sort">The column to sort by, or null for the order they were added in.</param>
    /// <returns>The page, or null when there's no such table.</returns>
    public async Task<AdminTableRows?> GetPageAsync(AdminDatabase database, string table, string? search, string? sort, bool descending, int page)
    {
        await using var connection = await schema.OpenAsync(database);
        if (await schema.FindTableAsync(connection, table) is not { } name)
            return null;

        var columns = await schema.ColumnsAsync(connection, database, name);
        var where = Where(columns, search);
        page = Math.Max(1, page);

        await using var count = connection.Command($"SELECT COUNT(*) FROM {Quote(name)} {where}");
        AddSearch(count, search);
        var matching = (long)(await count.ExecuteScalarAsync())!;

        await using var select = connection.Command(
            $"SELECT rowid, {SelectList(columns)} FROM {Quote(name)} {where} " +
            $"ORDER BY {OrderBy(columns, sort, descending)} LIMIT {PageSize} OFFSET {(page - 1) * PageSize}");
        AddSearch(select, search);

        return new AdminTableRows(name, columns, await ReadRowsAsync(select, columns), matching, page, PageSize);
    }

    /// <returns>The row, or null when it or its table isn't there.</returns>
    public async Task<AdminRow?> GetRowAsync(AdminDatabase database, string table, long rowID)
    {
        await using var connection = await schema.OpenAsync(database);
        if (await schema.FindTableAsync(connection, table) is not { } name)
            return null;

        var columns = await schema.ColumnsAsync(connection, database, name);
        await using var select = connection.Command($"SELECT rowid, {SelectList(columns)} FROM {Quote(name)} WHERE rowid = $id");
        select.Parameters.AddWithValue("$id", rowID);

        return (await ReadRowsAsync(select, columns)).FirstOrDefault();
    }

    /// <summary>
    /// Changes a row to what the form says (see <see cref="AdminForm"/>), or adds one.
    /// </summary>
    /// <param name="rowID">The row to change, or null for a new one.</param>
    /// <returns>Why it couldn't, or null once it has.</returns>
    public async Task<string?> SaveAsync(AdminDatabase database, string table, long? rowID, IFormCollection form)
    {
        await using var connection = await schema.OpenAsync(database);
        if (await schema.FindTableAsync(connection, table) is not { } name)
            return "That table isn't there anymore.";

        var columns = await schema.ColumnsAsync(connection, database, name);
        if (AdminForm.Read(columns, form, isNew: rowID is null, out var values) is { } error)
            return error;
        // The form had nothing of the row's to change
        if (rowID is not null && values.Count == 0)
            return null;

        await using var command = rowID is { } id ? Update(connection, name, values, id) : Insert(connection, name, values);
        try
        {
            var changed = await command.ExecuteNonQueryAsync();
            return rowID is not null && changed == 0 ? "That row isn't there anymore." : null;
        }
        catch (SqliteException e)
        {
            return Explain(e);
        }
    }

    /// <summary>
    /// Deletes a row, and whatever the database deletes with it (a fight's enemies, say).
    /// </summary>
    /// <returns>Why it couldn't, or null once it has.</returns>
    public async Task<string?> DeleteAsync(AdminDatabase database, string table, long rowID)
    {
        await using var connection = await schema.OpenAsync(database);
        if (await schema.FindTableAsync(connection, table) is not { } name)
            return "That table isn't there anymore.";

        await using var command = connection.Command($"DELETE FROM {Quote(name)} WHERE rowid = $id");
        command.Parameters.AddWithValue("$id", rowID);
        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (SqliteException e)
        {
            return Explain(e);
        }
    }

    private static SqliteCommand Update(SqliteConnection connection, string table, IReadOnlyList<AdminValue> values, long rowID)
    {
        var assignments = values.Select((value, i) => $"{Quote(value.Column)} = {Parameter(i)}");
        var command = connection.Command($"UPDATE {Quote(table)} SET {string.Join(", ", assignments)} WHERE rowid = $id");
        command.Parameters.AddWithValue("$id", rowID);
        return WithValues(command, values);
    }

    private static SqliteCommand Insert(SqliteConnection connection, string table, IReadOnlyList<AdminValue> values)
    {
        if (values.Count == 0)
            return connection.Command($"INSERT INTO {Quote(table)} DEFAULT VALUES");

        var names = string.Join(", ", values.Select(v => Quote(v.Column)));
        var parameters = string.Join(", ", values.Select((_, i) => Parameter(i)));
        return WithValues(connection.Command($"INSERT INTO {Quote(table)} ({names}) VALUES ({parameters})"), values);
    }

    private static string Parameter(int index) => $"$p{index}";

    private static SqliteCommand WithValues(SqliteCommand command, IReadOnlyList<AdminValue> values)
    {
        for (var i = 0; i < values.Count; i++)
            command.Parameters.AddWithValue(Parameter(i), values[i].Value);
        return command;
    }

    private static string Where(IReadOnlyList<AdminColumn> columns, string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? ""
            : "WHERE " + string.Join(" OR ", columns.Where(c => !c.IsBlob).Select(c => $"CAST({Quote(c.Name)} AS TEXT) LIKE $search"));

    private static void AddSearch(SqliteCommand command, string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
            command.Parameters.AddWithValue("$search", $"%{search.Trim()}%");
    }

    private static string OrderBy(IReadOnlyList<AdminColumn> columns, string? sort, bool descending)
    {
        var sorted = columns.FirstOrDefault(c => string.Equals(c.Name, sort, StringComparison.OrdinalIgnoreCase));
        return $"{(sorted is null ? "rowid" : Quote(sorted.Name))} {(descending ? "DESC" : "ASC")}";
    }

    // Reads what "SELECT rowid, {SelectList}" gave back
    private static async Task<IReadOnlyList<AdminRow>> ReadRowsAsync(SqliteCommand select, IReadOnlyList<AdminColumn> columns)
    {
        var rows = new List<AdminRow>();
        await using var reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(ReadRow(reader, columns));
        return rows;
    }

    private static AdminRow ReadRow(SqliteDataReader reader, IReadOnlyList<AdminColumn> columns)
    {
        var values = new object?[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            // A file was selected as its length
            var value = Value(reader, i + 1);
            values[i] = columns[i].IsBlob && value is long length ? new AdminBlob(length) : value;
        }
        return new AdminRow(reader.GetInt64(0), values);
    }
}

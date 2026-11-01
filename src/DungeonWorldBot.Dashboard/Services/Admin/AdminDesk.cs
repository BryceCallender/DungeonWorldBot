using System.Globalization;
using DungeonWorldBot.Dashboard.Models.Admin;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// The DM changing anything at all in the databases, a row at a time or with SQL. Unlike the other desks it goes
/// around the bot's rules, straight to the tables, so it can fix what they can't.
/// </summary>
/// <remarks>
/// Rows are found by SQLite's rowid, which every table the bot makes has. Foreign keys are on, so deleting a fight
/// takes its enemies with it, as it does in the bot, and a row can't point at one that isn't there. The bot's model
/// says which numbers are enums or yes/no, so they're picked from a list rather than typed.
/// </remarks>
public class AdminDesk(DashboardDatabases databases)
{
    public const int PageSize = 50;
    public const int MaxQueryRows = 500;

    private readonly Lazy<IReadOnlyDictionary<ColumnKey, ColumnHint>> _gameHints = new(() =>
    {
        using var context = new DungeonWorldContext(databases.WritableGame());
        return Hints(context);
    });

    private readonly Lazy<IReadOnlyDictionary<ColumnKey, ColumnHint>> _canvasHints = new(() =>
    {
        using var context = new CanvasContext(new DbContextOptionsBuilder<CanvasContext>().UseSqlite($"Data Source={databases.CanvasPath}").Options);
        return Hints(context);
    });

    public async Task<IReadOnlyList<AdminTable>> GetTablesAsync(AdminDatabase database)
    {
        await using var connection = await OpenAsync(database);
        var tables = new List<AdminTable>();
        foreach (var name in await TableNamesAsync(connection))
            tables.Add(new AdminTable(name, await ScalarAsync(connection, $"SELECT COUNT(*) FROM {Quote(name)}")));
        return tables;
    }

    /// <returns>The table's columns, or null when there's no such table.</returns>
    public async Task<IReadOnlyList<AdminColumn>?> GetColumnsAsync(AdminDatabase database, string table)
    {
        await using var connection = await OpenAsync(database);
        return await FindTableAsync(connection, table) is { } name ? await ColumnsAsync(connection, database, name) : null;
    }

    /// <param name="search">Text to find in any column, or null for every row.</param>
    /// <param name="sort">The column to sort by, or null for the order they were added in.</param>
    /// <returns>The page, or null when there's no such table.</returns>
    public async Task<AdminTableRows?> GetPageAsync(AdminDatabase database, string table, string? search, string? sort, bool descending, int page)
    {
        await using var connection = await OpenAsync(database);
        if (await FindTableAsync(connection, table) is not { } name)
            return null;

        var columns = await ColumnsAsync(connection, database, name);
        var where = "";
        if (!string.IsNullOrWhiteSpace(search))
            where = "WHERE " + string.Join(" OR ", columns.Where(c => !c.IsBlob).Select(c => $"CAST({Quote(c.Name)} AS TEXT) LIKE $search"));

        var order = columns.FirstOrDefault(c => string.Equals(c.Name, sort, StringComparison.OrdinalIgnoreCase)) is { } sorted
            ? Quote(sorted.Name)
            : "rowid";
        page = Math.Max(1, page);

        await using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {Quote(name)} {where}";
        AddSearch(count, search);
        var matching = (long)(await count.ExecuteScalarAsync())!;

        await using var select = connection.CreateCommand();
        select.CommandText = $"SELECT rowid, {SelectList(columns)} FROM {Quote(name)} {where} " +
                             $"ORDER BY {order} {(descending ? "DESC" : "ASC")} LIMIT {PageSize} OFFSET {(page - 1) * PageSize}";
        AddSearch(select, search);

        var rows = new List<AdminRow>();
        await using var reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(ReadRow(reader, columns));

        return new AdminTableRows(name, columns, rows, matching, page, PageSize);
    }

    /// <returns>The row, or null when it or its table isn't there.</returns>
    public async Task<AdminRow?> GetRowAsync(AdminDatabase database, string table, long rowID)
    {
        await using var connection = await OpenAsync(database);
        if (await FindTableAsync(connection, table) is not { } name)
            return null;

        var columns = await ColumnsAsync(connection, database, name);
        await using var select = connection.CreateCommand();
        select.CommandText = $"SELECT rowid, {SelectList(columns)} FROM {Quote(name)} WHERE rowid = $id";
        select.Parameters.AddWithValue("$id", rowID);

        await using var reader = await select.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadRow(reader, columns) : null;
    }

    /// <summary>
    /// Changes a row to what the form says, or adds one. Each column's value is in "v:{column}" (several for flags,
    /// added together), and "null:{column}" empties it. Files aren't in the form, so they stay as they are.
    /// </summary>
    /// <param name="rowID">The row to change, or null for a new one.</param>
    /// <returns>Why it couldn't, or null once it has.</returns>
    public async Task<string?> SaveAsync(AdminDatabase database, string table, long? rowID, IFormCollection form)
    {
        await using var connection = await OpenAsync(database);
        if (await FindTableAsync(connection, table) is not { } name)
            return "That table isn't there anymore.";

        var columns = await ColumnsAsync(connection, database, name);
        await using var command = connection.CreateCommand();
        var assigned = new List<string>();

        foreach (var column in columns.Where(c => !c.IsBlob))
        {
            var key = $"v:{column.Name}";
            if (!form.ContainsKey(key))
                continue;

            object value;
            if (form.ContainsKey($"null:{column.Name}"))
            {
                value = DBNull.Value;
            }
            else
            {
                var text = column.Flags ? null : form[key].ToString().Replace("\r\n", "\n");
                // A new row leaves blank what SQLite fills in itself, like its ID
                if (rowID is null && string.IsNullOrEmpty(text) && (column.HasDefault || column.PrimaryKey) && !column.Flags)
                    continue;

                if (column.Flags)
                {
                    long sum = 0;
                    foreach (var bit in form[key])
                    {
                        if (!long.TryParse(bit, out var part))
                            return $"{column.Name} takes whole numbers.";
                        sum |= part;
                    }
                    value = sum;
                }
                else if (column.IsInteger)
                {
                    if (string.IsNullOrWhiteSpace(text) && !column.NotNull && !column.PrimaryKey)
                        value = DBNull.Value;
                    else if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                        value = number;
                    // Discord IDs past the top of a long are stored wrapped around, as the bot stores them
                    else if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var big))
                        value = unchecked((long)big);
                    else
                        return $"{column.Name} takes a whole number.";
                }
                else if (column.IsReal)
                {
                    if (string.IsNullOrWhiteSpace(text) && !column.NotNull)
                        value = DBNull.Value;
                    else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                        value = number;
                    else
                        return $"{column.Name} takes a number.";
                }
                else
                {
                    value = text!;
                }
            }

            var parameter = $"$p{assigned.Count}";
            command.Parameters.AddWithValue(parameter, value);
            assigned.Add(rowID is null ? Quote(column.Name) : $"{Quote(column.Name)} = {parameter}");
        }

        if (rowID is { } id)
        {
            if (assigned.Count == 0)
                return null;
            command.CommandText = $"UPDATE {Quote(name)} SET {string.Join(", ", assigned)} WHERE rowid = $id";
            command.Parameters.AddWithValue("$id", id);
        }
        else
        {
            command.CommandText = assigned.Count == 0
                ? $"INSERT INTO {Quote(name)} DEFAULT VALUES"
                : $"INSERT INTO {Quote(name)} ({string.Join(", ", assigned)}) VALUES ({string.Join(", ", command.Parameters.Cast<SqliteParameter>().Select(p => p.ParameterName))})";
        }

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
        await using var connection = await OpenAsync(database);
        if (await FindTableAsync(connection, table) is not { } name)
            return "That table isn't there anymore.";

        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {Quote(name)} WHERE rowid = $id";
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

    /// <summary>
    /// Runs whatever SQL the DM typed, and gives back the first rows it read (up to <see cref="MaxQueryRows"/>) or how
    /// many it changed.
    /// </summary>
    public async Task<AdminQueryResult> RunAsync(AdminDatabase database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return AdminQueryResult.Failed("Type some SQL to run.");

        await using var connection = await OpenAsync(database);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        try
        {
            var columns = new List<string>();
            var rows = new List<IReadOnlyList<object?>>();
            var truncated = false;

            int changed;
            await using (var reader = await command.ExecuteReaderAsync())
            {
                // Only the first statement that reads anything is shown, but every one of them runs
                do
                {
                    if (reader.FieldCount == 0 || columns.Count > 0)
                    {
                        while (await reader.ReadAsync()) { }
                        continue;
                    }

                    for (var i = 0; i < reader.FieldCount; i++)
                        columns.Add(reader.GetName(i));
                    while (await reader.ReadAsync())
                    {
                        if (rows.Count == MaxQueryRows)
                        {
                            truncated = true;
                            continue;
                        }
                        var values = new object?[reader.FieldCount];
                        for (var i = 0; i < reader.FieldCount; i++)
                            values[i] = Value(reader, i);
                        rows.Add(values);
                    }
                } while (await reader.NextResultAsync());

                changed = reader.RecordsAffected;
            }

            return new AdminQueryResult(columns, rows, columns.Count == 0 ? Math.Max(changed, 0) : null, truncated, null);
        }
        catch (SqliteException e)
        {
            return AdminQueryResult.Failed(e.Message);
        }
    }

    private async Task<SqliteConnection> OpenAsync(AdminDatabase database)
    {
        var path = database == AdminDatabase.Canvas ? databases.CanvasPath : databases.GamePath;
        var connection = new SqliteConnection($"Data Source={path};Mode=ReadWrite;Foreign Keys=True");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<IReadOnlyList<string>> TableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    // Table names go into the SQL itself, so only ones that are really there are used, spelled as the database has them
    private static async Task<string?> FindTableAsync(SqliteConnection connection, string table) =>
        (await TableNamesAsync(connection)).FirstOrDefault(n => string.Equals(n, table, StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<AdminColumn>> ColumnsAsync(SqliteConnection connection, AdminDatabase database, string table)
    {
        var hints = (database == AdminDatabase.Canvas ? _canvasHints : _gameHints).Value;

        // Which columns point at another table's ID, so they can link to the row
        var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var keys = connection.CreateCommand())
        {
            keys.CommandText = $"PRAGMA foreign_key_list({Quote(table)})";
            await using var reader = await keys.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                references.TryAdd(reader.GetString(reader.GetOrdinal("from")), reader.GetString(reader.GetOrdinal("table")));
        }

        var columns = new List<AdminColumn>();
        await using var info = connection.CreateCommand();
        info.CommandText = $"PRAGMA table_info({Quote(table)})";
        await using (var reader = await info.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(reader.GetOrdinal("name"));
                var hint = hints.GetValueOrDefault(new ColumnKey(table, name));
                columns.Add(new AdminColumn(
                    name,
                    reader.GetString(reader.GetOrdinal("type")),
                    reader.GetInt64(reader.GetOrdinal("notnull")) == 1,
                    reader.GetInt64(reader.GetOrdinal("pk")) > 0,
                    !reader.IsDBNull(reader.GetOrdinal("dflt_value")),
                    hint?.Choices ?? [],
                    hint?.Flags ?? false,
                    references.GetValueOrDefault(name)));
            }
        }

        return columns;
    }

    // Files are only measured, so a page of characters doesn't carry all their pictures
    private static string SelectList(IReadOnlyList<AdminColumn> columns) =>
        string.Join(", ", columns.Select(c => c.IsBlob ? $"length({Quote(c.Name)})" : Quote(c.Name)));

    private static AdminRow ReadRow(SqliteDataReader reader, IReadOnlyList<AdminColumn> columns)
    {
        var values = new object?[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var value = Value(reader, i + 1);
            values[i] = columns[i].IsBlob && value is long length ? new AdminBlob(length) : value;
        }
        return new AdminRow(reader.GetInt64(0), values);
    }

    private static object? Value(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal) switch
        {
            byte[] bytes => new AdminBlob(bytes.Length),
            var value => value,
        };

    private static void AddSearch(SqliteCommand command, string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
            command.Parameters.AddWithValue("$search", $"%{search.Trim()}%");
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static string Explain(SqliteException e) => e.SqliteErrorCode switch
    {
        // SQLITE_CONSTRAINT: already taken, left empty, or pointing at a row that isn't there
        19 => $"The database said no: {e.Message.Replace("SQLite Error 19: ", "")}",
        _ => e.Message,
    };

    /// <summary>
    /// Which columns hold enums or yes/no, from the model the bot builds its tables with.
    /// </summary>
    private static IReadOnlyDictionary<ColumnKey, ColumnHint> Hints(DbContext context)
    {
        var hints = new Dictionary<ColumnKey, ColumnHint>();
        foreach (var entity in context.GetService<IDesignTimeModel>().Model.GetEntityTypes())
        {
            if (entity.GetTableName() is not { } table)
                continue;

            var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                if (property.GetColumnName(store) is not { } column)
                    continue;

                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                ColumnHint? hint = null;
                if (type == typeof(bool))
                {
                    hint = new ColumnHint([new AdminChoice("No", "0"), new AdminChoice("Yes", "1")], false);
                }
                else if (type.IsEnum)
                {
                    // Most are stored as their number; a few (a canvas's kind) by name
                    var byName = (property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType()) == typeof(string);
                    var choices = Enum.GetValues(type).Cast<object>()
                        .Select(v => new AdminChoice(v.ToString()!, byName ? v.ToString()! : Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)))
                        .DistinctBy(c => c.Value)
                        .ToList();
                    hint = new ColumnHint(choices, type.IsDefined(typeof(FlagsAttribute), false));
                }

                if (hint is not null)
                    hints.TryAdd(new ColumnKey(table, column), hint);
            }
        }

        return hints;
    }

    private record ColumnKey(string Table, string Column)
    {
        public virtual bool Equals(ColumnKey? other) =>
            other is not null
            && string.Equals(Table, other.Table, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Column, other.Column, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode() => HashCode.Combine(Table.ToUpperInvariant(), Column.ToUpperInvariant());
    }

    private record ColumnHint(IReadOnlyList<AdminChoice> Choices, bool Flags);
}

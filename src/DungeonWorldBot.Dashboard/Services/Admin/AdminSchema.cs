using System.Globalization;
using DungeonWorldBot.Dashboard.Models.Admin;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using static DungeonWorldBot.Dashboard.Services.Admin.AdminSql;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// What's in the databases the admin pages open: their tables, and each table's columns as SQLite has them. The bot's
/// model says which numbers are enums or yes/no, so they're picked from a list rather than typed.
/// </summary>
public class AdminSchema(DashboardDatabases databases)
{
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

    /// <summary>
    /// Opens a database to change. Foreign keys are on, so deleting a fight takes its enemies with it, as it does in
    /// the bot, and a row can't point at one that isn't there.
    /// </summary>
    public async Task<SqliteConnection> OpenAsync(AdminDatabase database)
    {
        var path = database == AdminDatabase.Canvas ? databases.CanvasPath : databases.GamePath;
        var connection = new SqliteConnection($"Data Source={path};Mode=ReadWrite;Foreign Keys=True");
        await connection.OpenAsync();
        return connection;
    }

    public async Task<IReadOnlyList<AdminTable>> GetTablesAsync(AdminDatabase database)
    {
        await using var connection = await OpenAsync(database);
        var tables = new List<AdminTable>();
        foreach (var name in await TableNamesAsync(connection))
        {
            await using var count = connection.Command($"SELECT COUNT(*) FROM {Quote(name)}");
            tables.Add(new AdminTable(name, (long)(await count.ExecuteScalarAsync())!));
        }
        return tables;
    }

    /// <returns>The table's columns, or null when there's no such table.</returns>
    public async Task<IReadOnlyList<AdminColumn>?> GetColumnsAsync(AdminDatabase database, string table)
    {
        await using var connection = await OpenAsync(database);
        return await FindTableAsync(connection, table) is { } name ? await ColumnsAsync(connection, database, name) : null;
    }

    /// <summary>
    /// A table's name spelled as the database has it. Names go into the SQL itself, so only ones found here are used.
    /// </summary>
    /// <returns>The name, or null when there's no such table.</returns>
    public async Task<string?> FindTableAsync(SqliteConnection connection, string table) =>
        (await TableNamesAsync(connection)).FirstOrDefault(n => string.Equals(n, table, StringComparison.OrdinalIgnoreCase));

    /// <param name="table">The table's name, from <see cref="FindTableAsync"/>.</param>
    public async Task<IReadOnlyList<AdminColumn>> ColumnsAsync(SqliteConnection connection, AdminDatabase database, string table)
    {
        var hints = (database == AdminDatabase.Canvas ? _canvasHints : _gameHints).Value;
        var references = await ReferencesAsync(connection, table);

        var columns = new List<AdminColumn>();
        await using var info = connection.Command($"PRAGMA table_info({Quote(table)})");
        await using var reader = await info.ExecuteReaderAsync();
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

        return columns;
    }

    private static async Task<IReadOnlyList<string>> TableNamesAsync(SqliteConnection connection)
    {
        await using var command = connection.Command("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            names.Add(reader.GetString(0));
        return names;
    }

    // Which columns point at another table's ID, so they can link to the row
    private static async Task<Dictionary<string, string>> ReferencesAsync(SqliteConnection connection, string table)
    {
        var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var keys = connection.Command($"PRAGMA foreign_key_list({Quote(table)})");
        await using var reader = await keys.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            references.TryAdd(reader.GetString(reader.GetOrdinal("from")), reader.GetString(reader.GetOrdinal("table")));
        return references;
    }

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
                if (property.GetColumnName(store) is { } column && Hint(property) is { } hint)
                    hints.TryAdd(new ColumnKey(table, column), hint);
            }
        }

        return hints;
    }

    private static ColumnHint? Hint(IProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (type == typeof(bool))
            return new ColumnHint([new AdminChoice("No", "0"), new AdminChoice("Yes", "1")], false);
        if (!type.IsEnum)
            return null;

        // Most are stored as their number; a few (a canvas's kind) by name
        var byName = (property.GetValueConverter()?.ProviderClrType ?? property.GetProviderClrType()) == typeof(string);
        var choices = Enum.GetValues(type).Cast<object>()
            .Select(v => new AdminChoice(v.ToString()!, byName ? v.ToString()! : Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)))
            .DistinctBy(c => c.Value)
            .ToList();
        return new ColumnHint(choices, type.IsDefined(typeof(FlagsAttribute), false));
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

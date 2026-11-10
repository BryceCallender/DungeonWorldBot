using DungeonWorldBot.Dashboard.Models.Admin;
using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// The admin page's box for running SQL as typed, for whatever changing a row at a time can't do.
/// </summary>
public class AdminConsole(AdminSchema schema)
{
    public const int MaxRows = 500;

    /// <summary>
    /// Runs whatever SQL the admin typed, and gives back the first rows it read (up to <see cref="MaxRows"/>) or how
    /// many it changed.
    /// </summary>
    public async Task<AdminQueryResult> RunAsync(AdminDatabase database, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return AdminQueryResult.Failed("Type some SQL to run.");

        await using var connection = await schema.OpenAsync(database);
        await using var command = connection.Command(sql);

        try
        {
            var columns = new List<string>();
            var rows = new List<IReadOnlyList<object?>>();
            var truncated = false;

            await using var reader = await command.ExecuteReaderAsync();
            // Only the first statement that reads anything is shown, but every one of them runs
            do
            {
                if (reader.FieldCount > 0 && columns.Count == 0)
                {
                    columns.AddRange(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));
                    truncated = await ReadRowsAsync(reader, rows);
                }
                while (await reader.ReadAsync()) { }
            } while (await reader.NextResultAsync());

            return new AdminQueryResult(columns, rows, columns.Count == 0 ? Math.Max(reader.RecordsAffected, 0) : null, truncated, null);
        }
        catch (SqliteException e)
        {
            return AdminQueryResult.Failed(e.Message);
        }
    }

    /// <returns>Whether there were more rows than it kept.</returns>
    private static async Task<bool> ReadRowsAsync(SqliteDataReader reader, List<IReadOnlyList<object?>> rows)
    {
        while (await reader.ReadAsync())
        {
            if (rows.Count == MaxRows)
                return true;
            rows.Add(Enumerable.Range(0, reader.FieldCount).Select(i => AdminSql.Value(reader, i)).ToList());
        }

        return false;
    }
}

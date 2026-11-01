namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// A row of a table, found by SQLite's rowid, with its values in the table's column order.
/// </summary>
public record AdminRow(long RowID, IReadOnlyList<object?> Values);

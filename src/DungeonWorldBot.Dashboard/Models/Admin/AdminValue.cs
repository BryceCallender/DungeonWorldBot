namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// What a form says to store in a column of a row.
/// </summary>
/// <param name="Value">What to store, DBNull for nothing.</param>
public record AdminValue(string Column, object Value);

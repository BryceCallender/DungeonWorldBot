namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// A table in one of the databases, with how many rows it has.
/// </summary>
public record AdminTable(string Name, long Rows);

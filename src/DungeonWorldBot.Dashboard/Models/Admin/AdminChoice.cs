namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// One of the values a column can hold, like a class for a character's class column.
/// </summary>
/// <param name="Label">What it means, e.g. "Ranger".</param>
/// <param name="Value">What's stored, e.g. "9", or the name itself for enums kept as text.</param>
public record AdminChoice(string Label, string Value);

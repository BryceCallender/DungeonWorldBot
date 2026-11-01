namespace DungeonWorldBot.Dashboard.Models.Health;

/// <summary>
/// How many of something happened on a day.
/// </summary>
public record DayCount(DateOnly Day, int Count);

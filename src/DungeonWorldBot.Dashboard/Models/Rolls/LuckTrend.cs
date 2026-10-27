namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Sessions">Their average dice (before modifiers) each session, oldest first, null where they didn't roll.</param>
public record LuckTrend(ulong UserID, IReadOnlyList<double?> Sessions);

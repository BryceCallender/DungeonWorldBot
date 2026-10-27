namespace DungeonWorldBot.Dashboard.Models.Rolls;

/// <param name="Name">e.g. "Session 12: Into the Sunken Abbey", when it was marked with /session start.</param>
public record SessionAwards(DateTime Started, IReadOnlyList<Award> Awards, string? Name = null);

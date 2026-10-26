namespace DungeonWorldBot.Dashboard.Rolls;

/// <param name="Players">How many people other than the DM rolled.</param>
/// <param name="Name">e.g. "Session 12: Into the Sunken Abbey", when it was marked with /session start.</param>
public record SessionRolls(DateTime Started, int Players, int Rolls, int Hits, int Misses, int Natural12s, int Natural2s, double AverageNatural, string? Name = null);

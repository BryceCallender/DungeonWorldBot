namespace DungeonWorldBot.Dashboard.Rolls;

/// <param name="Players">How many people other than the DM rolled.</param>
/// <param name="Misses">Each miss marks XP.</param>
public sealed record SessionRolls(DateTime Started, int Players, int Rolls, int Hits, int Misses, int Natural12s, int Natural2s, double AverageNatural);

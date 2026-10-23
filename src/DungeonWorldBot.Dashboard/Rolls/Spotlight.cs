namespace DungeonWorldBot.Dashboard.Rolls;

/// <summary>
/// Who has had the spotlight in a fight. Dungeon World has no initiative, so this is the DM's reminder of who's been
/// quiet instead of a turn order.
/// </summary>
/// <param name="Party">The living characters, quietest first.</param>
/// <param name="Rolls">How many rolls the party has made in the fight.</param>
/// <param name="Misses">The party's misses in the fight. Each one is a chance for the DM to make a move.</param>
public record Spotlight(IReadOnlyList<SpotlightLine> Party, int Rolls, int Misses);

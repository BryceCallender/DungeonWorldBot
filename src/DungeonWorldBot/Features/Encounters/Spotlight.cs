namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// Who has had the spotlight in a fight. Dungeon World has no initiative, so this is a reminder of who's been quiet
/// instead of a turn order.
/// </summary>
/// <param name="Party">The living characters, quietest first.</param>
/// <param name="Allies">The NPCs with the party who are still up, quietest first.</param>
/// <param name="Enemies">The enemies still standing, quietest first.</param>
/// <param name="Rolls">How many rolls the party has made in the fight.</param>
/// <param name="Misses">The party's misses in the fight. Each one is a chance for the DM to make a move.</param>
public record Spotlight(
    IReadOnlyList<SpotlightLine> Party,
    IReadOnlyList<SpotlightLine> Allies,
    IReadOnlyList<SpotlightLine> Enemies,
    int Rolls,
    int Misses);

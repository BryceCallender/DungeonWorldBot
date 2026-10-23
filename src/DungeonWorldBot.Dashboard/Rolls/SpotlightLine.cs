using DungeonWorldBot.Dashboard.Party;

namespace DungeonWorldBot.Dashboard.Rolls;

/// <summary>
/// How much of a fight one character has had, going by their rolls since it started.
/// </summary>
/// <param name="LastRolled">When they last rolled in the fight, or null if they haven't.</param>
/// <param name="LastMove">The move they last rolled for, if it was one.</param>
/// <param name="Quiet">Whether they're due the spotlight: everyone else has had a turn since theirs, or they've had none.</param>
public record SpotlightLine(CharacterView Character, int Rolls, int Misses, DateTimeOffset? LastRolled, string? LastMove, bool Quiet);

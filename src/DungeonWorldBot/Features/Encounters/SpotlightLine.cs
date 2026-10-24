using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// How much of a fight one character or enemy has had.
/// </summary>
/// <param name="CharacterID">The player's character, if it's one.</param>
/// <param name="Actions">How many times they've acted in the fight.</param>
/// <param name="Last">What they did last, or null if they haven't acted.</param>
/// <param name="Quiet">Whether they're due the spotlight: everyone else on their side has acted since they did, or
/// they haven't acted while someone else has.</param>
public record SpotlightLine(string Name, Snowflake? CharacterID, int Actions, int Misses, SpotlightAction? Last, bool Quiet);

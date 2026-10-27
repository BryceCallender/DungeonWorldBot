namespace DungeonWorldBot.Dashboard.Models.Encounters;

/// <summary>
/// How a move made from the board went, for everyone looking at it.
/// </summary>
/// <param name="PersonID">Who made it, so only they're shown why it failed.</param>
/// <param name="Actor">The token that made it.</param>
/// <param name="Target">The token it was aimed at.</param>
/// <param name="Summary">A few words to show over the target, e.g. "Miss" or "-4".</param>
/// <param name="Error">Why the bot couldn't make it, if it couldn't.</param>
public record MoveResult(string PersonID, string Actor, string Target, string? Summary, string? Error);

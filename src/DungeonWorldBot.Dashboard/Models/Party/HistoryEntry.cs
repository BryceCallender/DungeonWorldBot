namespace DungeonWorldBot.Dashboard.Models.Party;

/// <summary>
/// One moment on a character's timeline.
/// </summary>
/// <param name="Text">What happened, e.g. "Reached level 4".</param>
/// <param name="Detail">More about it, like a note's text or a roll's dice, if there's more to say.</param>
/// <param name="Url">Where to see more, like the fight, if anywhere.</param>
public record HistoryEntry(DateTimeOffset At, HistoryKind Kind, string Text, string? Detail = null, string? Url = null);

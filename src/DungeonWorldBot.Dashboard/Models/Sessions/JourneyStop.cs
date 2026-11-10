using DungeonWorldBot.Dashboard.Models.Party;

namespace DungeonWorldBot.Dashboard.Models.Sessions;

/// <summary>
/// Somewhere the party, or some of it, went during a session, and what happened while they were there.
/// </summary>
/// <param name="Place">The scene made or fought on there, or the place the DM gave a stop added by hand. It's how
/// the stop is found on the map.</param>
/// <param name="At">When they got there.</param>
/// <param name="Who">The characters who went, by name.</param>
/// <param name="WholeParty">Whether everyone went, so there's no need to name them.</param>
/// <param name="Moments">Oldest first: the fights, and what changed on the party.</param>
/// <param name="LoggedID">For a stop the DM added by hand, its ID, for taking it away again; null for a scene's or a
/// fight's.</param>
public record JourneyStop(
    string Place,
    DateTimeOffset At,
    IReadOnlyList<string> Who,
    bool WholeParty,
    IReadOnlyList<HistoryEntry> Moments,
    string? LoggedID = null)
{
    /// <summary>
    /// Whether there was a fight there, rather than only a scene made or a stop added by hand.
    /// </summary>
    public bool Fought => Moments.Any(m => m.Kind == HistoryKind.Fight);
}

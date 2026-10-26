namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// Some of what's happened in a fight, newest first: the latest, or what came before the oldest already shown.
/// </summary>
/// <param name="Total">How many things have happened in the whole fight.</param>
/// <param name="HasMore">Whether anything happened before the oldest of these.</param>
public record TimelinePage(IReadOnlyList<TimelineEntry> Entries, int Total, bool HasMore);

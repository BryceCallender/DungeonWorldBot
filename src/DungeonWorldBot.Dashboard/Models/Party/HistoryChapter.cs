namespace DungeonWorldBot.Dashboard.Models.Party;

/// <summary>
/// A stretch of a character's timeline: a session, or a day between them.
/// </summary>
/// <param name="Title">e.g. "Session 12: Into the Sunken Abbey", or "Between sessions".</param>
/// <param name="Url">The session's page, for a session.</param>
/// <param name="Entries">Newest first.</param>
public record HistoryChapter(string Title, string? Url, DateTimeOffset Start, IReadOnlyList<HistoryEntry> Entries);

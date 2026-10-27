using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Sessions;

namespace DungeonWorldBot.Dashboard.Models.Sessions;

/// <summary>
/// A session marked with /session start, with its number and how its dice have gone so far.
/// </summary>
public record SessionSummary(GameSession Session, int Number, SessionRecap Recap)
{
    public string Name => Session.Title is { } title ? $"Session {Number}: {title}" : $"Session {Number}";

    public string Url => $"session/{Number}";

    public bool Live => Session.EndedAt is null;

    public TimeSpan Length => (Session.EndedAt ?? DateTimeOffset.UtcNow) - Session.StartedAt;
}

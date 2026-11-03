using System.Collections.Concurrent;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Configuration;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters.Creation;

/// <summary>
/// Each player's character creation in progress, so running /character create again picks up where they left off.
/// Kept in memory, so a restart of the bot loses them.
/// </summary>
[Singleton]
public class CreationSessions(IDiceRoller dice, IConfiguration configuration)
{
    // Long enough to wander off and look something up, short enough that an abandoned draft doesn't come back weeks later
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<Snowflake, CreationSession> _sessions = new();

    // Off unless the TrackWeight setting turns it on, since the table doesn't count weight
    private readonly bool _trackWeight = configuration.GetValue("TrackWeight", false);

    /// <returns>The player's session in progress, or a new one if they have none.</returns>
    public CreationSession Resume(Snowflake userId, out bool resumed)
    {
        RemoveExpired();

        resumed = _sessions.TryGetValue(userId, out var session);
        if (session is null)
        {
            var draft = new CharacterDraft { TrackWeight = _trackWeight };
            draft.ResetGear(dice);
            session = _sessions.GetOrAdd(userId, new CreationSession(draft));
        }

        session.LastTouched = DateTimeOffset.UtcNow;
        return session;
    }

    /// <returns>The player's session in progress, or null if they have none or it expired.</returns>
    public CreationSession? Find(Snowflake userId)
    {
        RemoveExpired();

        if (!_sessions.TryGetValue(userId, out var session))
            return null;

        session.LastTouched = DateTimeOffset.UtcNow;
        return session;
    }

    /// <returns>Whether this call ended the session, so a double-clicked Create only saves once.</returns>
    public bool End(Snowflake userId) => _sessions.TryRemove(userId, out _);

    private void RemoveExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - Expiry;
        foreach (var (userId, session) in _sessions)
        {
            if (session.LastTouched < cutoff)
                _sessions.TryRemove(userId, out _);
        }
    }
}

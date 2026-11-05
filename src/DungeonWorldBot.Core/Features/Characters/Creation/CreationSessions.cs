using System.Collections.Concurrent;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters.Creation;

/// <param name="Resumed">Whether it was already in progress, rather than just started.</param>
public record ResumedSession(CreationSession Session, bool Resumed);

/// <summary>
/// Each player's character creation in progress, so running /character create again picks up where they left off.
/// Kept in memory, so a restart of the bot loses them.
/// </summary>
[Singleton]
public class CreationSessions(IDiceRoller dice, IOptions<BotSettings> settings, TimeProvider time)
{
    // Long enough to wander off and look something up, short enough that an abandoned draft doesn't come back weeks later
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<Snowflake, CreationSession> _sessions = new();


    /// <returns>The player's session in progress, or a new one if they have none.</returns>
    public ResumedSession Resume(Snowflake userId)
    {
        RemoveExpired();

        var resumed = _sessions.TryGetValue(userId, out var session);
        if (session is null)
        {
            var draft = new CharacterDraft { TrackWeight = settings.Value.TrackWeight };
            draft.ResetGear(dice);
            session = _sessions.GetOrAdd(userId, new CreationSession(draft));
        }

        session.LastTouched = time.GetUtcNow();
        return new ResumedSession(session, resumed);
    }

    /// <returns>The player's session in progress, or null if they have none or it expired.</returns>
    public CreationSession? Find(Snowflake userId)
    {
        RemoveExpired();

        if (!_sessions.TryGetValue(userId, out var session))
            return null;

        session.LastTouched = time.GetUtcNow();
        return session;
    }

    /// <returns>Whether this call ended the session, so a double-clicked Create only saves once.</returns>
    public bool End(Snowflake userId) => _sessions.TryRemove(userId, out _);

    private void RemoveExpired()
    {
        var cutoff = time.GetUtcNow() - Expiry;
        foreach (var (userId, session) in _sessions)
        {
            if (session.LastTouched < cutoff)
                _sessions.TryRemove(userId, out _);
        }
    }
}

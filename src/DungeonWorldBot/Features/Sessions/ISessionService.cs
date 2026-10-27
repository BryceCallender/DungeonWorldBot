using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Sessions;

/// <summary>
/// How a session went at the table, from the game chat's 2d6 rolls while it was on.
/// </summary>
/// <param name="Players">Each player's rolls, most first. The DM's are counted too.</param>
/// <param name="Fights">The names of fights started while it was on, first first.</param>
public record SessionRecap(int Rolls, int Hits, int Misses, int Natural12s, int Natural2s, IReadOnlyList<PlayerSessionRolls> Players, IReadOnlyList<string> Fights);

public record PlayerSessionRolls(Snowflake UserID, int Rolls, int Misses);

public interface ISessionService
{
    /// <summary>
    /// The session going on now, or null.
    /// </summary>
    Task<GameSession?> GetCurrentAsync();

    /// <summary>
    /// Every session, oldest first.
    /// </summary>
    Task<List<GameSession>> GetAllAsync();

    /// <summary>
    /// The session before this one, or null if it's the first.
    /// </summary>
    Task<GameSession?> GetPreviousAsync(GameSession session);

    /// <summary>
    /// Which session this is, counting from 1.
    /// </summary>
    Task<int> NumberOfAsync(GameSession session);

    /// <returns>The new session, or null if one's already going on.</returns>
    Task<GameSession?> StartAsync(string? title = null);

    /// <returns>The session that was going on, now ended, or null if none was.</returns>
    Task<GameSession?> EndAsync();

    Task<SessionRecap> RecapAsync(GameSession session);
}

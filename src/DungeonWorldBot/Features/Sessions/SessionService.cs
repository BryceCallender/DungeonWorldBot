using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Sessions;

[Scoped<ISessionService>]
public class SessionService : ISessionService
{
    private readonly DungeonWorldContext _context;
    private readonly IRollStatsService _rollStats;

    public SessionService(DungeonWorldContext context, IRollStatsService rollStats)
    {
        _context = context;
        _rollStats = rollStats;
    }

    public Task<GameSession?> GetCurrentAsync() =>
        _context.Sessions.Where(s => s.EndedAt == null).OrderByDescending(s => s.ID).FirstOrDefaultAsync();

    // SQLite can't order by dates stored as text with an offset, but sessions are only ever added in order, so IDs do
    public Task<List<GameSession>> GetAllAsync() => _context.Sessions.OrderBy(s => s.ID).ToListAsync();

    public Task<GameSession?> GetPreviousAsync(GameSession session) =>
        _context.Sessions.Where(s => s.ID < session.ID).OrderByDescending(s => s.ID).FirstOrDefaultAsync();

    public async Task<int> NumberOfAsync(GameSession session) => await _context.Sessions.CountAsync(s => s.ID <= session.ID);

    public async Task<GameSession?> StartAsync(string? title = null)
    {
        if (await GetCurrentAsync() is not null)
            return null;

        var session = new GameSession
        {
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            StartedAt = DateTimeOffset.UtcNow,
        };
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<GameSession?> EndAsync()
    {
        if (await GetCurrentAsync() is not { } session)
            return null;

        session.EndedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<SessionRecap> RecapAsync(GameSession session)
    {
        var from = session.StartedAt;
        var to = session.EndedAt ?? DateTimeOffset.UtcNow;

        // When a roll was made comes from its message ID, which SQL can't read, so they're sorted out here
        var rolls = _rollStats.GameChatChannelID is { } gameChat
            ? (await _context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync())
                .Where(r => r.MessageID.Timestamp >= from && r.MessageID.Timestamp <= to)
                .ToList()
            : [];
        var dice = rolls.Select(r => new MoveDice(r.First, r.Second, r.Total)).ToList();

        var fights = (await _context.Encounters.AsNoTracking().OrderBy(e => e.ID).Select(e => new { e.Name, e.StartedAt }).ToListAsync())
            .Where(e => e.StartedAt >= from && e.StartedAt <= to)
            .Select(e => e.Name)
            .ToList();

        return new SessionRecap(
            dice.Count,
            dice.Count(d => d.Outcome != MoveOutcome.Miss),
            dice.Count(d => d.Outcome == MoveOutcome.Miss),
            dice.Count(d => d.IsNatural12),
            dice.Count(d => d.IsNatural2),
            rolls
                .GroupBy(r => r.UserID)
                .Select(g => new PlayerSessionRolls(
                    g.Key,
                    g.Count(),
                    g.Count(r => new MoveDice(r.First, r.Second, r.Total).Outcome == MoveOutcome.Miss)))
                .OrderByDescending(p => p.Rolls)
                .ToList(),
            fights);
    }
}

using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Sessions;

[Scoped<ISessionService>]
public class SessionService(DungeonWorldContext context, IRollStatsService rollStats, TimeProvider time) : ISessionService
{
    /// <summary>
    /// A break in rolling this long means the next roll is a new session, so a game that runs past midnight is one session.
    /// </summary>
    public static readonly TimeSpan Gap = TimeSpan.FromHours(8);

    // Fewer rolls than this on their own aren't a game night, just someone trying the bot
    public const int MinimumBackfillRolls = 5;

    public Task<GameSession?> GetCurrentAsync() =>
        context.Sessions.Where(s => s.EndedAt == null).OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync();

    // Numbered by when they started, not by ID, since backfilled nights are added after the ones they came before
    public Task<List<GameSession>> GetAllAsync() => context.Sessions.OrderBy(s => s.StartedAt).ToListAsync();

    public Task<GameSession?> GetPreviousAsync(GameSession session) =>
        context.Sessions.Where(s => s.StartedAt < session.StartedAt).OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync();

    public Task<int> NumberOfAsync(GameSession session) => context.Sessions.CountAsync(s => s.StartedAt <= session.StartedAt);

    public async Task<GameSession?> StartAsync(string? title = null)
    {
        if (await GetCurrentAsync() is not null)
            return null;

        var session = new GameSession
        {
            Title = Tidy(title),
            StartedAt = time.GetUtcNow(),
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    public async Task<GameSession?> EndAsync()
    {
        if (await GetCurrentAsync() is not { } session)
            return null;

        session.EndedAt = time.GetUtcNow();
        await context.SaveChangesAsync();
        return session;
    }

    public async Task RenameAsync(GameSession session, string? title)
    {
        session.Title = Tidy(title);
        await context.SaveChangesAsync();
    }

    public async Task<SessionRecap> RecapAsync(GameSession session)
    {
        var from = session.StartedAt;
        var to = session.EndedAt ?? time.GetUtcNow();

        var rolls = RollsDuring(session) is { } during ? await during.ToListAsync() : [];
        var dice = rolls.Select(r => MoveDice.Of(r)).ToList();

        var fights = await context.Encounters.AsNoTracking()
            .Where(e => e.StartedAt >= from && e.StartedAt <= to)
            .OrderBy(e => e.StartedAt)
            .Select(e => e.Name)
            .ToListAsync();

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
                    g.Count(r => MoveDice.Of(r).Outcome == MoveOutcome.Miss)))
                .OrderByDescending(p => p.Rolls)
                .ToList(),
            fights);
    }

    public async Task<List<GameSession>> BackfillAsync()
    {
        if (rollStats.GameChatChannelID is not { } gameChat)
            return [];

        var marked = await context.Sessions.AsNoTracking().OrderBy(s => s.StartedAt).ToListAsync();
        var rolledAt = (await context.Rolls.Where(r => r.ChannelID == gameChat).OldestFirst().Select(r => r.MessageID).ToListAsync())
            .Select(id => id.Timestamp)
            .ToList();

        List<List<DateTimeOffset>> nights = [];
        List<DateTimeOffset>? night = null;
        foreach (var at in rolledAt)
        {
            if (marked.Any(s => at >= s.StartedAt && at <= (s.EndedAt ?? DateTimeOffset.MaxValue)))
            {
                night = null;
                continue;
            }

            // A marked session between two rolls splits them too, even when it had no rolls of its own
            if (night is null || at - night[^1] > Gap || marked.Any(s => s.StartedAt > night[^1] && s.StartedAt < at))
                nights.Add(night = []);

            night.Add(at);
        }

        var added = nights
            .Where(n => n.Count >= MinimumBackfillRolls)
            .Select(n => new GameSession { StartedAt = n[0], EndedAt = n[^1] })
            .ToList();
        if (added.Count == 0)
            return added;

        context.Sessions.AddRange(added);
        await context.SaveChangesAsync();
        return added;
    }

    public async Task<int> CountRollsAsync(GameSession session) =>
        RollsDuring(session) is { } during ? await during.CountAsync() : 0;

    /// <summary>
    /// The game chat's rolls during the session, or null when there's no game chat: like the stats, only its rolls count.
    /// </summary>
    private IQueryable<RollRecord>? RollsDuring(GameSession session) =>
        rollStats.GameChatChannelID is { } gameChat
            ? context.Rolls
                .AsNoTracking()
                .Where(r => r.ChannelID == gameChat)
                .Since(session.StartedAt)
                .Until(session.EndedAt ?? time.GetUtcNow())
            : null;

    private static string? Tidy(string? title) => string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}

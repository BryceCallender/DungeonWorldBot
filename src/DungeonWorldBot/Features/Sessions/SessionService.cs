using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Sessions;

[Scoped<ISessionService>]
public class SessionService(DungeonWorldContext context, IRollStatsService rollStats) : ISessionService
{
    /// <summary>
    /// A break in rolling this long means the next roll is a new session, so a game that runs past midnight is one session.
    /// </summary>
    public static readonly TimeSpan Gap = TimeSpan.FromHours(8);

    // Fewer rolls than this on their own aren't a game night, just someone trying the bot
    public const int MinimumBackfillRolls = 5;

    public Task<GameSession?> GetCurrentAsync() =>
        context.Sessions.Where(s => s.EndedAt == null).OrderByDescending(s => s.ID).FirstOrDefaultAsync();

    // SQLite can't order by dates stored as text with an offset, but sessions are only ever added in order, so IDs do
    public Task<List<GameSession>> GetAllAsync() => context.Sessions.OrderBy(s => s.ID).ToListAsync();

    public Task<GameSession?> GetPreviousAsync(GameSession session) =>
        context.Sessions.Where(s => s.ID < session.ID).OrderByDescending(s => s.ID).FirstOrDefaultAsync();

    public async Task<int> NumberOfAsync(GameSession session) => await context.Sessions.CountAsync(s => s.ID <= session.ID);

    public async Task<GameSession?> StartAsync(string? title = null)
    {
        if (await GetCurrentAsync() is not null)
            return null;

        var session = new GameSession
        {
            Title = Tidy(title),
            StartedAt = DateTimeOffset.UtcNow,
        };
        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    public async Task<GameSession?> EndAsync()
    {
        if (await GetCurrentAsync() is not { } session)
            return null;

        session.EndedAt = DateTimeOffset.UtcNow;
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
        var to = session.EndedAt ?? DateTimeOffset.UtcNow;

        // When a roll was made comes from its message ID, which SQL can't read, so they're sorted out here
        var rolls = rollStats.GameChatChannelID is { } gameChat
            ? (await context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync())
                .Where(r => r.MessageID.Timestamp >= from && r.MessageID.Timestamp <= to)
                .ToList()
            : [];
        var dice = rolls.Select(r => MoveDice.Of(r)).ToList();

        var fights = (await context.Encounters.AsNoTracking().OrderBy(e => e.ID).Select(e => new { e.Name, e.StartedAt }).ToListAsync())
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
                    g.Count(r => MoveDice.Of(r).Outcome == MoveOutcome.Miss)))
                .OrderByDescending(p => p.Rolls)
                .ToList(),
            fights);
    }

    public async Task<List<GameSession>> BackfillAsync()
    {
        if (rollStats.GameChatChannelID is not { } gameChat)
            return [];

        var marked = await context.Sessions.AsNoTracking().OrderBy(s => s.ID).ToListAsync();
        var rolledAt = (await context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).Select(r => r.MessageID).ToListAsync())
            .Select(id => id.Timestamp)
            .Order()
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

        // Sessions are numbered by ID, so they're all written again in the order they started. Nothing else points at
        // a session's ID.
        var all = marked.Concat(added).OrderBy(s => s.StartedAt).ToList();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Sessions.ExecuteDeleteAsync();
        context.ChangeTracker.Clear();
        for (var i = 0; i < all.Count; i++)
            all[i].ID = i + 1;
        context.Sessions.AddRange(all);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return added;
    }

    private static string? Tidy(string? title) => string.IsNullOrWhiteSpace(title) ? null : title.Trim();
}

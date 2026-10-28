using DungeonWorldBot.Dashboard.Models.Sessions;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Sessions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Sessions;

/// <summary>
/// The sessions marked with /session start and end, read with the bot's own rules. None when the bot's database
/// predates them.
/// </summary>
public class SessionReader(ISessionService sessions, DungeonWorldContext context)
{
    /// <summary>
    /// The fights started during a session, first first.
    /// </summary>
    public async Task<List<Encounter>> GetFightsAsync(SessionSummary summary)
    {
        var from = summary.Session.StartedAt;
        var to = summary.Session.EndedAt ?? DateTimeOffset.MaxValue;
        // SQLite can't compare dates stored with an offset, so they're sorted out here
        return (await context.Encounters.AsNoTracking().OrderBy(e => e.ID).ToListAsync())
            .Where(e => e.StartedAt >= from && e.StartedAt <= to)
            .ToList();
    }

    /// <summary>
    /// The session a fight was started during, if any.
    /// </summary>
    public Task<SessionSummary?> GetDuringAsync(Encounter fight) => Safely(async () =>
        (await sessions.GetAllAsync()).FirstOrDefault(s => fight.StartedAt >= s.StartedAt && fight.StartedAt <= (s.EndedAt ?? DateTimeOffset.MaxValue))
            is { } session ? await SummaryAsync(session) : null);

    public Task<SessionSummary?> GetCurrentAsync() => Safely(async () =>
        await sessions.GetCurrentAsync() is { } current ? await SummaryAsync(current) : null);

    /// <param name="number">Counting from 1, as the bot numbers them.</param>
    public Task<SessionSummary?> GetAsync(int number) => Safely(async () =>
        number >= 1 && (await sessions.GetAllAsync()).ElementAtOrDefault(number - 1) is { } session ? await SummaryAsync(session) : null);

    public Task<SessionSummary?> GetPreviousAsync(SessionSummary summary) => Safely(async () =>
        await sessions.GetPreviousAsync(summary.Session) is { } previous ? await SummaryAsync(previous) : null);

    /// <summary>
    /// Every session, the one going on first, then newest first.
    /// </summary>
    public async Task<List<SessionSummary>> GetAllAsync() =>
        await Safely(async () =>
        {
            var all = await sessions.GetAllAsync();
            List<SessionSummary> summaries = [];
            // Numbered by the order they started in, as the bot numbers them
            foreach (var (session, i) in all.Select((s, i) => (s, i)))
                summaries.Add(new SessionSummary(session, i + 1, await sessions.RecapAsync(session)));
            return summaries.OrderByDescending(s => s.Live).ThenByDescending(s => s.Number).ToList();
        }) ?? [];

    /// <summary>
    /// How many sessions have been marked.
    /// </summary>
    public async Task<int> CountAsync() => (await Safely(async () => (int?)(await sessions.GetAllAsync()).Count)) ?? 0;

    private async Task<SessionSummary> SummaryAsync(GameSession session) =>
        new(session, await sessions.NumberOfAsync(session), await sessions.RecapAsync(session));

    private static async Task<T?> Safely<T>(Func<Task<T?>> read)
    {
        try
        {
            return await read();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table"))
        {
            return default;
        }
    }
}

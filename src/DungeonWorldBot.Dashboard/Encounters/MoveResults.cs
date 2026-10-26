using System.Collections.Concurrent;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Dashboard.Live;
using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Encounters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// How a move made from the board went, for everyone looking at it.
/// </summary>
/// <param name="PersonID">Who made it, so only they're shown why it failed.</param>
/// <param name="Actor">The token that made it.</param>
/// <param name="Target">The token it was aimed at.</param>
/// <param name="Summary">A few words to show over the target, e.g. "Miss" or "-4".</param>
/// <param name="Error">Why the bot couldn't make it, if it couldn't.</param>
public record MoveResult(string PersonID, string Actor, string Target, string? Summary, string? Error);

/// <summary>
/// A move asked for from a board, waiting for the bot to roll it.
/// </summary>
public record WaitingMove(int CanvasID, string PersonID, string Actor, string Target, DateTimeOffset AskedAt);

/// <summary>
/// Tells boards how the moves made from them went, once the bot has rolled them: it notices through
/// <see cref="DatabaseWatcher"/>, like the rest of the dashboard.
/// </summary>
/// <remarks>
/// Only moves asked for since the dashboard started are followed. One the bot never gets to, as while it's off, is
/// given up on a little after the bot would itself.
/// </remarks>
public class MoveResults(
    DatabaseWatcher watcher,
    IServiceScopeFactory scopes,
    IHubContext<CanvasHub, ICanvasClient> hub,
    ILogger<MoveResults> logger) : BackgroundService
{
    private static readonly TimeSpan CheckTimeoutsEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan GiveUpAfter = MoveRequestWorker.GiveUpAfter + TimeSpan.FromSeconds(10);
    private const string BotAway = "The bot didn't roll it. Is it running?";

    private readonly ConcurrentDictionary<int, WaitingMove> _waiting = new();

    public void Follow(int requestID, WaitingMove move) => _waiting[requestID] = move;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.WhenAny(watcher.NextChange, Task.Delay(CheckTimeoutsEvery, stoppingToken));
            if (stoppingToken.IsCancellationRequested || _waiting.IsEmpty)
                continue;

            try
            {
                await SendRolledAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "Couldn't check on moves made from the board");
            }
        }
    }

    private async Task SendRolledAsync(CancellationToken ct)
    {
        var ids = _waiting.Keys.ToList();
        await using var scope = scopes.CreateAsyncScope();
        var rolled = await scope.ServiceProvider.GetRequiredService<DungeonWorldContext>().MoveRequests.AsNoTracking()
            .Where(r => ids.Contains(r.ID) && r.ResolvedAt != null)
            .Select(r => new { r.ID, r.Summary, r.Error })
            .ToListAsync(ct);

        foreach (var request in rolled)
        {
            if (_waiting.TryRemove(request.ID, out var move))
                await SendAsync(move, request.Summary, request.Error);
        }

        foreach (var (id, move) in _waiting)
        {
            if (DateTimeOffset.UtcNow - move.AskedAt > GiveUpAfter && _waiting.TryRemove(id, out _))
                await SendAsync(move, null, BotAway);
        }
    }

    private Task SendAsync(WaitingMove move, string? summary, string? error) =>
        hub.Clients.Group(CanvasHub.Group(move.CanvasID)).MoveResolved(new MoveResult(move.PersonID, move.Actor, move.Target, summary, error));
}

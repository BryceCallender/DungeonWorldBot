using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Rest;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// Rolls moves made from fights' scenes on the dashboard, which can only ask for them (see <see cref="MoveRequest"/>),
/// and tells game chat about each, as /move would.
/// </summary>
/// <remarks>
/// Each is marked taken before it's rolled, so a move is never made twice, even if something goes wrong partway.
/// </remarks>
public class MoveRequestWorker(IServiceScopeFactory scopes, ILogger<MoveRequestWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Moves asked for longer ago than this, as while the bot was off, are dropped rather than all landing at once.
    /// </summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(1);

    public const string TooLate = "The bot wasn't around to roll it in time. Try again.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RollWaitingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                // The database busy or mid-migration: the next look will likely do
                logger.LogWarning(e, "Couldn't check for moves made on the dashboard");
            }

            await Task.Delay(Interval, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }
    }

    private async Task RollWaitingAsync(CancellationToken ct)
    {
        List<int> waiting;
        await using (var scope = scopes.CreateAsyncScope())
        {
            waiting = await scope.ServiceProvider.GetRequiredService<DungeonWorldContext>().MoveRequests
                .Where(r => r.ResolvedAt == null)
                .OrderBy(r => r.ID)
                .Select(r => r.ID)
                .ToListAsync(ct);
        }

        // Each in its own scope, so one move's changes are all saved (or not) before the next sees them
        foreach (var id in waiting)
            await RollAsync(id, ct);
    }

    private async Task RollAsync(int id, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<DungeonWorldContext>();

        if (await context.MoveRequests.FindAsync([id], ct) is not { ResolvedAt: null } request)
            return;

        var now = DateTimeOffset.UtcNow;
        request.ResolvedAt = now;
        if (now - request.RequestedAt > GiveUpAfter)
        {
            request.Error = TooLate;
            await context.SaveChangesAsync(ct);
            return;
        }

        // Taken before rolling, so it's never rolled again
        await context.SaveChangesAsync(ct);

        ProcessedMove move;
        try
        {
            var processed = await services.GetRequiredService<MoveRequestProcessor>().ProcessAsync(request);
            if (processed.Move is null)
            {
                request.Error = processed.Error;
                await context.SaveChangesAsync(ct);
                return;
            }

            move = processed.Move;
            request.Summary = move.Summary;
            await context.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't roll move {ID} made on the dashboard", id);
            request.Error = "Something went wrong rolling it. Check game chat before trying again.";
            await context.SaveChangesAsync(ct);
            return;
        }

        await TellGameChatAsync(services, move, ct);
    }

    /// <summary>
    /// Posts the move in game chat, and counts a player's roll in their stats as /move would. The move stands either way.
    /// </summary>
    private async Task TellGameChatAsync(IServiceProvider services, ProcessedMove move, CancellationToken ct)
    {
        var rollStats = services.GetRequiredService<IRollStatsService>();
        if (rollStats.GameChatChannelID is not { } gameChat)
            return;

        var sent = await services.GetRequiredService<IDiscordRestChannelAPI>().CreateMessageAsync(gameChat, embeds: new[] { move.Embed }, ct: ct);
        if (!sent.IsSuccess)
        {
            logger.LogWarning("Couldn't tell game chat about a move made on the dashboard: {Reason}", sent.Error.Message);
            return;
        }

        if (move.Action.ID != 0)
        {
            move.Action.MessageID = sent.Entity.ID;
            await services.GetRequiredService<DungeonWorldContext>().SaveChangesAsync(ct);
        }

        if (move.Roller is { } roller && move.Dice is { } dice)
            await rollStats.RecordAsync(sent.Entity.ID, gameChat, roller, dice, RollLabel.Of(move.Embed));
    }
}

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
public class MoveRequestWorker(IServiceScopeFactory scopes, ILogger<MoveRequestWorker> logger, TimeProvider time) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            do
            {
                await LookAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down
        }
    }

    private async Task LookAsync(CancellationToken ct)
    {
        try
        {
            await RollWaitingAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The database busy or mid-migration: the next look will likely do
            logger.LogWarning(e, "Couldn't check for moves made on the dashboard");
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

        // Each in its own scope, so one move's changes are all saved (or not) before the next sees them. Shutting down
        // stops between moves, never partway through one, so none is left taken but never rolled.
        foreach (var id in waiting)
        {
            if (ct.IsCancellationRequested)
                return;
            await RollAsync(id);
        }
    }

    private async Task RollAsync(int id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<DungeonWorldContext>();

        if (await context.MoveRequests.FindAsync(id) is not { ResolvedAt: null } request)
            return;

        var now = time.GetUtcNow();
        request.ResolvedAt = now;
        if (now - request.RequestedAt > MoveRequestRules.GiveUpAfter)
        {
            request.Error = MoveRequestRules.TooLate;
            await context.SaveChangesAsync();
            return;
        }

        // Taken before rolling, so it's never rolled again
        await context.SaveChangesAsync();

        ProcessedMove move;
        try
        {
            var processed = await services.GetRequiredService<MoveRequestProcessor>().ProcessAsync(request);
            if (processed.Move is null)
            {
                request.Error = processed.Error;
                await context.SaveChangesAsync();
                return;
            }

            move = processed.Move;
            request.Summary = move.Summary;
            await context.SaveChangesAsync();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't roll move {ID} made on the dashboard", id);
            request.Error = "Something went wrong rolling it. Check game chat before trying again.";
            await context.SaveChangesAsync();
            return;
        }

        await TellGameChatAsync(services, move);
    }

    /// <summary>
    /// Posts the move in game chat, and counts a player's roll in their stats as /move would. The move stands either way.
    /// </summary>
    private async Task TellGameChatAsync(IServiceProvider services, ProcessedMove move)
    {
        var rollStats = services.GetRequiredService<IRollStatsService>();
        if (rollStats.GameChatChannelID is not { } gameChat)
            return;

        // Dramatic rolls land a moment late in game chat too, like they do from Discord
        var reveal = services.GetRequiredService<RollReveal>();
        var drama = move is { Roller: { } who, Dice: { } rolled }
            ? await reveal.DramaOfAsync(who, rolled, move.Action.Action, move.Outcome, move.RollerHealth)
            : null;
        var channelAPI = services.GetRequiredService<IDiscordRestChannelAPI>();
        var sent = await reveal.SendAsync(move.Embed, drama, move.Action.Actor, move.Action.Action,
            e => channelAPI.CreateMessageAsync(gameChat, embeds: new[] { e }));
        if (!sent.IsSuccess)
        {
            logger.LogWarning("Couldn't tell game chat about a move made on the dashboard: {Reason}", sent.Error.Message);
            return;
        }

        if (move.Action.ID != 0)
        {
            move.Action.MessageID = sent.Entity.ID;
            await services.GetRequiredService<DungeonWorldContext>().SaveChangesAsync();
        }

        if (move.Roller is { } roller && move.Dice is { } dice)
            await rollStats.RecordAsync(sent.Entity.ID, gameChat, roller, dice, RollLabel.Of(move.Embed));
    }
}

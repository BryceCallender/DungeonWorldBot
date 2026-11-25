using System.Text.Json;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DungeonWorldBot.Features.Bridge;

/// <summary>
/// Runs the commands asked for from the dashboard (see <see cref="CommandRequest"/>) through <see cref="CommandBridge"/>,
/// oldest first, and notes how each went for the dashboard.
/// </summary>
/// <remarks>
/// Each is marked taken before it's run, so a command is never run twice, even if something goes wrong partway.
/// </remarks>
public class CommandRequestWorker(IServiceScopeFactory scopes, ILogger<CommandRequestWorker> logger, TimeProvider time) : BackgroundService
{
    // Suggestions fill a dropdown as it opens, so they're looked for often
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(400);

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
            await RunWaitingAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The database busy or mid-migration: the next look will likely do
            logger.LogWarning(e, "Couldn't check for commands run on the dashboard");
        }
    }

    private async Task RunWaitingAsync(CancellationToken ct)
    {
        List<int> waiting;
        await using (var scope = scopes.CreateAsyncScope())
        {
            waiting = await scope.ServiceProvider.GetRequiredService<DungeonWorldContext>().CommandRequests
                .Where(r => r.ResolvedAt == null)
                .OrderBy(r => r.ID)
                .Select(r => r.ID)
                .ToListAsync(ct);
        }

        // Each in its own scope, so one command's changes are all saved (or not) before the next sees them. Shutting
        // down stops between commands, never partway through one.
        foreach (var id in waiting)
        {
            if (ct.IsCancellationRequested)
                return;
            await RunAsync(id);
        }
    }

    private async Task RunAsync(int id)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<DungeonWorldContext>();

        if (await context.CommandRequests.FindAsync(id) is not { ResolvedAt: null } request)
            return;

        var now = time.GetUtcNow();
        request.ResolvedAt = now;
        if (now - request.RequestedAt > CommandRequestRules.GiveUpAfter)
        {
            request.Error = CommandRequestRules.TooLate;
            await context.SaveChangesAsync();
            return;
        }

        // Taken before running, so it's never run again
        await context.SaveChangesAsync();

        BridgeResult result;
        try
        {
            var bridge = services.GetRequiredService<CommandBridge>();
            result = request.Kind == CommandRequestKind.Suggest
                ? await bridge.SuggestAsync(request, CancellationToken.None)
                : await bridge.RunAsync(request, CancellationToken.None);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Couldn't run /{Command} from the dashboard", request.Command);
            result = BridgeResult.Failed("Something went wrong running it. Check game chat before trying again.");
        }

        // The command may have saved changes of its own through this context, so the request is looked up fresh
        await using var after = scopes.CreateAsyncScope();
        var saving = after.ServiceProvider.GetRequiredService<DungeonWorldContext>();
        if (await saving.CommandRequests.FindAsync(id) is not { } resolved)
            return;

        resolved.Error = result.Error;
        resolved.Reply = result.Reply is null ? null : JsonSerializer.Serialize(result.Reply, BridgeJson.Default.CommandReply);
        await saving.SaveChangesAsync();
    }
}

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// Writes the bot's <see cref="BotStatus"/> beside its database every <see cref="Interval"/>, and once more as it shuts
/// down, so the dashboard can tell whether it's up and what it's been doing.
/// </summary>
/// <param name="databasePath">The bot's SanaSolem.db.</param>
public class BotStatusWriter(string databasePath, CommandTally tally, RecentErrors errors, ILogger<BotStatusWriter> logger, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly DateTimeOffset _startedAt = time.GetUtcNow();
    private readonly string _version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                await WriteAsync(null);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down, which writes once more as it stops
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await WriteAsync(time.GetUtcNow());
    }

    private async Task WriteAsync(DateTimeOffset? stoppedAt)
    {
        var status = new BotStatus(
            _startedAt,
            time.GetUtcNow(),
            stoppedAt,
            _version,
            Environment.MachineName,
            Process.GetCurrentProcess().WorkingSet64,
            tally.Counts,
            errors.Latest);

        // Written beside it and moved over, so the dashboard never reads half a file
        var path = BotStatus.PathFor(databasePath);
        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(status, BotStatusJson.Default.BotStatus));
            File.Move(temporary, path, overwrite: true);
        }
        // Never worth taking the bot down over: a background service that throws stops the whole host
        catch (Exception e)
        {
            logger.LogWarning(e, "Couldn't write the bot's status to {Path}", path);
        }
    }
}

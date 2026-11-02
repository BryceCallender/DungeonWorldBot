using System.Diagnostics;
using DungeonWorldBot.Dashboard.Models.Health;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Rolls;
using DungeonWorldBot.Infrastructure.Telemetry;
using Microsoft.Data.Sqlite;

namespace DungeonWorldBot.Dashboard.Services.Health;

/// <summary>
/// How the bot and the dashboard are doing. The bot runs on its own, maybe on the Pi, so what it says about itself comes
/// from the status file it writes beside its database (see <see cref="BotStatusWriter"/>).
/// </summary>
public class HealthReader(
    DashboardDatabases databases,
    RecentErrors errors,
    RollReader rolls,
    IConfiguration configuration)
{
    public const int RollDays = 14;

    // Two missed beats, so one slow write doesn't call it down
    private static readonly TimeSpan Silence = BotStatusWriter.Interval * 2 + TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset StartedAt = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public async Task<HealthReport> ReadAsync()
    {
        var bot = await BotStatus.ReadAsync(databases.GamePath);
        var state = bot switch
        {
            null => BotState.Unknown,
            { StoppedAt: not null } => BotState.Stopped,
            _ when DateTimeOffset.UtcNow - bot.LastBeat > Silence => BotState.Down,
            _ => BotState.Online,
        };

        return new HealthReport(
            state,
            bot,
            StartedAt,
            Process.GetCurrentProcess().WorkingSet64,
            errors.Latest,
            [File("Game", databases.GamePath), File("Scenes", databases.CanvasPath)],
            await RollsByDayAsync(),
            TelemetryUrl());
    }

    private async Task<IReadOnlyList<DayCount>> RollsByDayAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var first = today.AddDays(-(RollDays - 1));

        IReadOnlyList<DateOnly> days;
        try
        {
            days = (await rolls.GetRollsAsync(first.ToDateTime(TimeOnly.MinValue), DateTimeOffset.Now))
                .Select(r => DateOnly.FromDateTime(r.MessageID.Timestamp.LocalDateTime))
                .ToList();
        }
        catch (SqliteException)
        {
            // No database yet, or the bot hasn't made the rolls table
            days = [];
        }

        var counts = days.CountBy(d => d).ToDictionary();
        return Enumerable.Range(0, RollDays)
            .Select(i => first.AddDays(i))
            .Select(day => new DayCount(day, counts.GetValueOrDefault(day)))
            .ToList();
    }

    /// <summary>
    /// Where the logs, traces and metrics can be looked at: TelemetryDashboardUrl, or the Aspire Dashboard's usual
    /// address when they're being sent to one on this machine.
    /// </summary>
    private string? TelemetryUrl() =>
        configuration["TelemetryDashboardUrl"]
        ?? (configuration[Telemetry.EndpointKey] is { } endpoint && endpoint.Contains("localhost") ? "http://localhost:18888" : null);

    private static DatabaseFile File(string name, string path)
    {
        var database = new FileInfo(path);
        if (!database.Exists)
            return new DatabaseFile(name, path, 0, 0, null);

        var journal = new FileInfo(path + "-wal");
        if (!journal.Exists)
            return new DatabaseFile(name, path, database.Length, 0, database.LastWriteTimeUtc);

        var lastWrite = journal.LastWriteTimeUtc > database.LastWriteTimeUtc ? journal.LastWriteTimeUtc : database.LastWriteTimeUtc;
        return new DatabaseFile(name, path, database.Length, journal.Length, lastWrite);
    }
}

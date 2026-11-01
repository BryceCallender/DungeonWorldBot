using DungeonWorldBot.Infrastructure.Telemetry;

namespace DungeonWorldBot.Dashboard.Models.Health;

/// <summary>
/// How the bot and the dashboard are doing, for the health page.
/// </summary>
/// <param name="Bot">What the bot last wrote about itself, or null if it never has.</param>
/// <param name="DashboardErrors">The dashboard's own last few errors, newest first.</param>
/// <param name="RollsByDay">The game chat's rolls on each of the last few days, oldest first.</param>
/// <param name="TelemetryUrl">Where to look at logs, traces and metrics, when they're being sent somewhere.</param>
public record HealthReport(
    BotState BotState,
    BotStatus? Bot,
    DateTimeOffset DashboardStartedAt,
    long DashboardMemoryBytes,
    IReadOnlyList<LoggedError> DashboardErrors,
    IReadOnlyList<DatabaseFile> Databases,
    IReadOnlyList<DayCount> RollsByDay,
    string? TelemetryUrl)
{
    /// <summary>
    /// Both of their errors together, newest first.
    /// </summary>
    public IReadOnlyList<SourcedError> Errors =>
        (Bot?.RecentErrors ?? []).Select(e => new SourcedError("Bot", e))
            .Concat(DashboardErrors.Select(e => new SourcedError("Dashboard", e)))
            .OrderByDescending(e => e.Error.At)
            .ToList();
}

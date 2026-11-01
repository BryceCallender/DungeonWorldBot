using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// The game's own numbers, on top of the runtime's: commands run and how long they took, and rolls made. Reported over
/// OpenTelemetry when it's on (see <see cref="Telemetry"/>), and free when it isn't.
/// </summary>
public static class BotMetrics
{
    public const string Name = "SanaSolem.Bot";

    public static readonly ActivitySource Source = new(Name);

    private static readonly Meter Meter = new(Name);

    private static readonly Counter<long> Commands =
        Meter.CreateCounter<long>("sanasolem.commands", "{command}", "Slash commands run, by command and whether they worked");

    private static readonly Histogram<double> CommandDuration =
        Meter.CreateHistogram<double>("sanasolem.command.duration", "ms", "How long slash commands took");

    private static readonly Counter<long> Rolls =
        Meter.CreateCounter<long>("sanasolem.rolls", "{roll}", "2d6 rolls recorded, by band");

    public static void CommandRan(string command, bool succeeded, TimeSpan took)
    {
        var tags = new TagList { { "command", command }, { "outcome", succeeded ? "success" : "failed" } };
        Commands.Add(1, tags);
        CommandDuration.Record(took.TotalMilliseconds, tags);
    }

    public static void Rolled(int total) =>
        Rolls.Add(1, new KeyValuePair<string, object?>("band", total >= 10 ? "10+" : total >= 7 ? "7-9" : "6-"));
}

using System.Collections.Concurrent;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// Every slash command run since the bot started, for its status file.
/// </summary>
public class CommandTally
{
    private readonly ConcurrentDictionary<string, Tally> _commands = new();

    public void Add(string command, bool succeeded, TimeSpan took) =>
        _commands.AddOrUpdate(
            command,
            _ => new Tally(1, succeeded ? 0 : 1, took.TotalMilliseconds),
            (_, t) => new Tally(t.Runs + 1, t.Failed + (succeeded ? 0 : 1), t.TotalMilliseconds + took.TotalMilliseconds));

    public IReadOnlyList<CommandCount> Counts => _commands
        .Select(c => new CommandCount(c.Key, c.Value.Runs, c.Value.Failed, c.Value.TotalMilliseconds / c.Value.Runs))
        .OrderByDescending(c => c.Runs)
        .ThenBy(c => c.Command)
        .ToList();

    private record Tally(int Runs, int Failed, double TotalMilliseconds);
}

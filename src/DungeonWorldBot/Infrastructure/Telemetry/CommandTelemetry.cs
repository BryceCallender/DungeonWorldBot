using System.Diagnostics;
using Remora.Commands.Trees.Nodes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// Times each slash command, from just before it runs to just after: a trace span for it, the metrics, and its count
/// for the status file. Remora runs both halves in the command's own scope, so they share one <see cref="Timing"/>.
/// </summary>
public static class CommandTelemetry
{
    /// <summary>
    /// The command running in this scope.
    /// </summary>
    public class Timing
    {
        public Stopwatch Clock { get; } = new();

        public Activity? Activity { get; set; }
    }

    public class Before(Timing timing) : IPreExecutionEvent
    {
        public Task<Result> BeforeExecutionAsync(ICommandContext context, CancellationToken ct = default)
        {
            timing.Activity = BotMetrics.Source.StartActivity($"/{NameOf(context)}");
            timing.Clock.Restart();
            return Task.FromResult(Result.Success);
        }
    }

    public class After(Timing timing, CommandTally tally) : IPostExecutionEvent
    {
        public Task<Result> AfterExecutionAsync(ICommandContext context, IResult commandResult, CancellationToken ct = default)
        {
            var took = timing.Clock.Elapsed;
            var name = NameOf(context);

            BotMetrics.CommandRan(name, commandResult.IsSuccess, took);
            tally.Add(name, commandResult.IsSuccess, took);

            if (timing.Activity is { } activity)
            {
                activity.SetTag("command", name);
                if (!commandResult.IsSuccess)
                    activity.SetStatus(ActivityStatusCode.Error, commandResult.Error?.Message);
                activity.Stop();
            }

            return Task.FromResult(Result.Success);
        }
    }

    /// <summary>
    /// A command's full name, groups and all, e.g. "dm npc add".
    /// </summary>
    public static string NameOf(ICommandContext context)
    {
        var parts = new List<string>();
        IChildNode? node = context.Command.Command.Node;
        while (node is not null)
        {
            parts.Insert(0, node.Key);
            node = node.Parent as IChildNode;
        }
        return string.Join(' ', parts);
    }
}

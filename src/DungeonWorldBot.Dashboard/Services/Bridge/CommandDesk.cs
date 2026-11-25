using System.Text.Json;
using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Bridge;
using DungeonWorldBot.Dashboard.Services.Party;
using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Bridge;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Services.Bridge;

/// <summary>
/// Runs the bot's slash commands from the dashboard: everything a player or the DM can do in Discord, for anyone who'd
/// rather tap than type. The dashboard can't run them itself, so it asks the bot (see <see cref="CommandRequest"/>),
/// which runs each as the player, by the same rules, and posts in game chat as usual.
/// </summary>
/// <remarks>
/// Players run commands as themselves (or anyone, with sign-in off), and only the DM sees the DM's commands. The bot
/// checks the DM's role again when it runs them, as it does in Discord.
/// </remarks>
public class CommandDesk(CommandCatalogReader catalog, GameWrites writes, DungeonWorldContext context, PartyReader party, TimeProvider time)
{
    public const int MaxValueLength = 1000;

    // A little after the bot itself gives up, so a command it never got to (as while it's off) doesn't wait forever
    private static readonly TimeSpan GiveUpAfter = CommandRequestRules.GiveUpAfter + TimeSpan.FromSeconds(10);
    private const string BotAway = "The bot didn't answer. Is it running?";
    private const string NoCatalog = "The bot hasn't said what it can do yet. Is it running?";

    /// <summary>
    /// The commands the viewer can run, and the party to point them at.
    /// </summary>
    public async Task<CommandMenu> MenuAsync(Viewer viewer)
    {
        var commands = (await catalog.ReadAsync())?.Commands.Where(c => viewer.CanRunGame || !c.GameMasterOnly).ToList() ?? [];
        var people = (await party.GetPartyAsync()).Select(c => new MenuPerson(c.ID, c.Name)).ToList();
        var playing = viewer.ID is { } me ? (await party.PlayedByAsync(me)).Playing?.ID.Value.ToString() : null;
        return new CommandMenu(commands, people, playing, viewer.CanRunGame);
    }

    public Task<CommandAsked> RunAsync(Viewer viewer, CommandAsk ask) => AskAsync(viewer, ask, CommandRequestKind.Run);

    public Task<CommandAsked> SuggestAsync(Viewer viewer, CommandAsk ask) => AskAsync(viewer, ask, CommandRequestKind.Suggest);

    /// <summary>
    /// How a command asked for went, or null if there's no such command the viewer may see.
    /// </summary>
    public async Task<CommandStatus?> StatusAsync(Viewer viewer, int requestID)
    {
        var request = await context.CommandRequests.AsNoTracking().FirstOrDefaultAsync(r => r.ID == requestID);
        if (request is null || !viewer.CanActAs(request.UserID.Value))
            return null;
        // The bot marks it taken before running it, and only says how it went once it's done
        if (request is { Error: null, Reply: null })
            return time.GetUtcNow() - request.RequestedAt > GiveUpAfter ? new CommandStatus(true, BotAway) : CommandStatus.Waiting;

        var reply = request.Reply is null ? null : JsonSerializer.Deserialize(request.Reply, BridgeJson.Default.CommandReply);
        return new CommandStatus(
            true,
            request.Error,
            reply is { Messages.Count: > 0 } ? CommandReplyHtml.For(reply) : null,
            reply?.Suggestions);
    }

    private async Task<CommandAsked> AskAsync(Viewer viewer, CommandAsk ask, CommandRequestKind kind)
    {
        if (await catalog.ReadAsync() is not { } known)
            return CommandAsked.Failed(NoCatalog);
        if (known.Commands.FirstOrDefault(c => c.Path.Equals(ask.Command.Trim(), StringComparison.OrdinalIgnoreCase)) is not { } command)
            return CommandAsked.Failed($"The bot has no /{ask.Command} command.");
        if (WhyNot(viewer, ask, command, kind) is { } problem)
            return CommandAsked.Failed(problem);

        // Only the options the command takes, by their own names
        var options = command.Options
            .Select(o => (o.Name, Value: ask.Options.FirstOrDefault(given => given.Key.Equals(o.Name, StringComparison.OrdinalIgnoreCase)).Value?.Trim()))
            .Where(o => !string.IsNullOrEmpty(o.Value))
            .ToDictionary(o => o.Name, o => o.Value!);

        await using var write = writes.Open();
        var requests = write.Context.CommandRequests;
        var user = new Snowflake(ask.As);
        if (kind == CommandRequestKind.Run && await StillWaitingAsync(requests, user))
            return CommandAsked.Failed("Still waiting on the last one. Give the bot a moment.");

        var request = new CommandRequest
        {
            RequestedAt = time.GetUtcNow(),
            RequestedBy = viewer.ID is { } id ? new Snowflake(id) : null,
            UserID = user,
            Kind = kind,
            Command = command.Path,
            Options = JsonSerializer.Serialize(options, BridgeJson.Default.DictionaryStringString),
            Focused = kind == CommandRequestKind.Suggest ? command.Options.FirstOrDefault(o => o.Name.Equals(ask.Focused, StringComparison.OrdinalIgnoreCase))?.Name : null,
        };
        requests.Add(request);
        await write.Context.SaveChangesAsync();
        return new CommandAsked(null, request.ID);
    }

    private static string? WhyNot(Viewer viewer, CommandAsk ask, CatalogCommand command, CommandRequestKind kind)
    {
        if (!viewer.CanActAs(ask.As))
            return "You can only run commands as yourself.";
        if (command.GameMasterOnly && !viewer.CanRunGame)
            return "Only the DM can do that.";
        if (ask.Options.Values.Any(v => v?.Length > MaxValueLength))
            return "That's too long.";
        if (kind == CommandRequestKind.Suggest && !command.Options.Any(o => o.Suggests && o.Name.Equals(ask.Focused, StringComparison.OrdinalIgnoreCase)))
            return "The bot has no suggestions for that.";
        return null;
    }

    private Task<bool> StillWaitingAsync(DbSet<CommandRequest> requests, Snowflake user)
    {
        var since = time.GetUtcNow() - CommandRequestRules.GiveUpAfter;
        return requests.AnyAsync(r => r.UserID == user && r.Kind == CommandRequestKind.Run && r.ResolvedAt == null && r.RequestedAt > since);
    }
}

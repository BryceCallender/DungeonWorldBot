using System.Text.Json;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remora.Commands.Results;
using Remora.Commands.Services;
using Remora.Commands.Tokenization;
using Remora.Commands.Trees;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Bridge;

/// <param name="Error">Why it couldn't be run, or null when it was (whatever the command itself said).</param>
public record BridgeResult(string? Error, CommandReply? Reply)
{
    public static BridgeResult Failed(string error, CommandReply? reply = null) => new(error, reply);
}

/// <summary>
/// Runs a slash command asked for from the dashboard (see <see cref="CommandRequest"/>) as the player it's for, as if
/// they'd typed it in game chat: it's found, checked (the DM's commands for the DM only) and run by Remora, with the
/// same events as any command, and its replies go to game chat. Those replies are noted (see
/// <see cref="ReplyCapture"/>) for the dashboard to show too.
/// </summary>
/// <remarks>
/// Needs a scope of its own for each request: the command's services read who's running it from the
/// <see cref="ContextInjectionService"/>, which this sets before anything in the scope asks.
/// </remarks>
[Scoped]
public class CommandBridge(
    IServiceProvider services,
    ContextInjectionService injection,
    CommandService commands,
    CommandCatalogBuilder catalog,
    ExecutionEventCollectorService events,
    IDiscordRestUserAPI users,
    IDiscordRestChannelAPI channels,
    IRollStatsService rollStats,
    IOptions<TokenizerOptions> tokenizer,
    IOptions<TreeSearchOptions> search,
    ILogger<CommandBridge> logger,
    TimeProvider time)
{
    private const string NoGameChat = "Game chat isn't set up, so there's nowhere for the bot to answer.";
    private const string SomethingWrong = "Something went wrong running it. Try again, and tell the DM if it keeps happening.";

    public async Task<BridgeResult> RunAsync(CommandRequest request, CancellationToken ct)
    {
        if (catalog.Find(request.Command) is null)
            return BridgeResult.Failed($"The dashboard can't run /{request.Command}.");
        if (await AsPlayerAsync(request.UserID, ct) is not { } player)
            return BridgeResult.Failed(NoGameChat);

        injection.Context = new DashboardMessageContext(player.Message, player.GuildID);
        var prepared = await commands.TryPrepareCommandAsync(
            PathOf(request), OptionsOf(request), services, tokenizer.Value, search.Value, treeName: null, ct);
        if (!prepared.IsSuccess)
            return BridgeResult.Failed(WhyNot(request, prepared));

        var context = new DashboardCommandContext(player.Message, player.GuildID, prepared.Entity);
        injection.Context = context;
        var before = await events.RunPreExecutionEvents(services, context, ct);
        if (!before.IsSuccess)
            return BridgeResult.Failed(WhyNot(request, before));

        var seen = ReplyCapture.Start();
        try
        {
            var executed = await commands.TryExecuteAsync(prepared.Entity, services, ct);
            var outcome = executed.IsSuccess ? executed.Entity : executed;
            await events.RunPostExecutionEvents(services, context, outcome, ct);

            var reply = CommandReply.Posted(Replies(seen));
            return outcome.IsSuccess ? new BridgeResult(null, reply) : BridgeResult.Failed(WhyNot(request, outcome), reply);
        }
        finally
        {
            ReplyCapture.Stop();
        }
    }

    /// <summary>
    /// What the bot suggests for one of a command's options, as it would while the player typed it in Discord.
    /// </summary>
    public async Task<BridgeResult> SuggestAsync(CommandRequest request, CancellationToken ct)
    {
        if (await AsPlayerAsync(request.UserID, ct) is not { } player)
            return BridgeResult.Failed(NoGameChat);

        // Set before the providers are made, since some ask who's typing as they're made
        injection.Context = new DashboardMessageContext(player.Message, player.GuildID);
        var option = catalog.Find(request.Command)?.Shape.Parameters
            .FirstOrDefault(p => p.HintName.Equals(request.Focused, StringComparison.OrdinalIgnoreCase));
        if (option?.Attributes.OfType<AutocompleteProviderAttribute>().FirstOrDefault() is not { } wanted
            || services.GetServices<IAutocompleteProvider>().FirstOrDefault(p => p.Identity == wanted.ProviderIdentity) is not { } provider)
            return BridgeResult.Failed("The bot has no suggestions for that.");

        var typed = OptionsOf(request).GetValueOrDefault(option.HintName)?.FirstOrDefault() ?? string.Empty;
        var suggested = await provider.GetSuggestionsAsync([], typed, ct);
        return new BridgeResult(null, CommandReply.Suggested(suggested
            .Select(s => new CatalogChoice(s.Name, s.Value.Value.ToString() ?? s.Name))
            .ToList()));
    }

    private record Player(IPartialMessage Message, Optional<Snowflake> GuildID);

    /// <summary>
    /// A message as if the player had typed the command in game chat, which is where its replies go.
    /// </summary>
    private async Task<Player?> AsPlayerAsync(Snowflake userID, CancellationToken ct)
    {
        if (rollStats.GameChatChannelID is not { } gameChat)
            return null;

        var channel = await channels.GetChannelAsync(gameChat, ct);
        var guild = channel.IsSuccess ? channel.Entity.GuildID : default;

        // A player who isn't in Discord any more (or a made-up one, in test data) still gets a name to be called by
        var user = await users.GetUserAsync(userID, ct);
        IUser author = user.IsSuccess ? user.Entity : new User(userID, "player", 0, default, null);

        var message = new PartialMessage(
            ID: DiscordSnowflake.New(((ulong)time.GetUtcNow().ToUnixTimeMilliseconds() - Constants.DiscordEpoch) << 22),
            ChannelID: gameChat,
            Author: new Optional<IUser>(author),
            Content: string.Empty);
        return new Player(message, guild);
    }

    private static IReadOnlyList<string> PathOf(CommandRequest request) =>
        request.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, IReadOnlyList<string>> OptionsOf(CommandRequest request)
    {
        var options = JsonSerializer.Deserialize(request.Options, BridgeJson.Default.DictionaryStringString) ?? [];
        return options
            .Where(o => !string.IsNullOrWhiteSpace(o.Value))
            .ToDictionary(o => o.Key, o => (IReadOnlyList<string>)[o.Value.Trim()], StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ReplyMessage> Replies(IEnumerable<IMessage> messages) =>
        messages.Select(m => new ReplyMessage(
            string.IsNullOrEmpty(m.Content) ? null : m.Content,
            m.Embeds.Select(ToReply).ToList())).ToList();

    private static ReplyEmbed ToReply(IEmbed embed) => new(
        embed.Title.OrDefault(null),
        embed.Description.OrDefault(null),
        embed.Fields.OrDefault([])?.Select(f => new ReplyField(f.Name, f.Value)).ToList() ?? [],
        embed.Footer.TryGet(out var footer) ? footer.Text : null,
        embed.Colour.TryGet(out var colour) ? colour.ToArgb() & 0xFFFFFF : null);

    /// <summary>
    /// The reason worth showing: the innermost one, like "Only the DM can do that" under "a condition wasn't met", but
    /// never an exception's, which is logged instead.
    /// </summary>
    private string WhyNot(CommandRequest request, IResult result)
    {
        var error = result.Error;
        for (var inner = result.Inner; inner is { IsSuccess: false, Error: not null }; inner = inner.Inner)
            error = inner.Error;

        switch (error)
        {
            case ExceptionError thrown:
                logger.LogError(thrown.Exception, "/{Command} from the dashboard threw", request.Command);
                return SomethingWrong;
            case ParameterParsingError parsing:
                return $"That isn't something {parsing.Parameter.ParameterShape.HintName} takes.";
            case CommandNotFoundError:
                return "Fill in everything it needs first.";
            case null:
                return SomethingWrong;
            default:
                return error.Message;
        }
    }
}

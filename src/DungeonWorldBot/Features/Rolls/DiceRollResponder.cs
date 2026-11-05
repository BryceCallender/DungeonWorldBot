using System.Drawing;
using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Infrastructure;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// Rolls dice for chat messages like ".2d6+1 sneak attack" or ".dex+1 dodge the trap".
/// </summary>
public class DiceRollResponder(
    IDiscordRestChannelAPI channelAPI,
    IDiceRoller diceRoller,
    ICharacterService characterService,
    IRollStatsService rollStatsService,
    RollReveal rollReveal)
    : IResponder<IMessageCreate>
{
    private static readonly Color RollColor = Color.Fuchsia;

    public async Task<Result> RespondAsync(IMessageCreate gatewayEvent, CancellationToken ct = default)
    {
        if (gatewayEvent.Author.IsBot.OrDefault(false))
            return Result.FromSuccess();

        // Anything that isn't a roll (e.g. "..." or ".hi") is just a normal message
        var request = DiceMessage.TryParse(gatewayEvent.Content);
        if (request is null)
            return Result.FromSuccess();

        // A plain pick from a list isn't a move, however it lands, so it needs no character
        var author = gatewayEvent.Author;
        var character = request.Stat is not null || request.Options is null
            ? await characterService.GetCharacterFromUserAsync(author.ID)
            : null;

        var (embed, roll) = request.Stat is not null
            ? BuildStatRoll(gatewayEvent, request, character)
            : BuildDiceRoll(gatewayEvent, request);

        if (embed is null)
            return Result.FromSuccess();

        var drama = roll is not null && request.Options is null
            ? await rollReveal.DramaOfAsync(author.ID, roll, RollLabel.Of(embed).Move, health: character?.Health)
            : null;

        var reply = await rollReveal.SendAsync(
            embed,
            drama,
            character?.Name ?? author.GlobalName.OrDefault(null) ?? author.Username,
            request.Label,
            e => channelAPI.CreateMessageAsync(
                gatewayEvent.ChannelID,
                embeds: new[] { e },
                messageReference: new MessageReference(MessageID: gatewayEvent.ID, FailIfNotExists: false),
                allowedMentions: new AllowedMentions(MentionRepliedUser: false),
                ct: ct),
            ct);

        if (!reply.IsSuccess)
            return Result.FromError(reply);

        if (roll is not null)
            await rollStatsService.RecordAsync(reply.Entity.ID, gatewayEvent.ChannelID, gatewayEvent.Author.ID, roll, RollLabel.Of(embed));

        return Result.FromSuccess();
    }
    /// <param name="Roll">The dice, when there are any to count toward roll stats.</param>
    private record BuiltRoll(Embed? Embed, DiceResult? Roll);


    private BuiltRoll BuildDiceRoll(IMessageCreate message, DiceMessage request)
    {
        if (!diceRoller.TryRoll(request.Expression!, out var roll))
            return new BuiltRoll(null, null);

        if (request.Options is not null)
            return new BuiltRoll(BuildPick(message, request, request.Options, roll), roll);

        var outcome = MoveRoller.OutcomeOf(roll);

        var embed = new Embed(
            Title: Title(message.Author, request.Label),
            Description: DiceHelper.BuildDiceResult(roll),
            Fields: outcome is { } band
                ? new List<IEmbedField> { new EmbedField(MoveRoller.Describe(band), MoveRoller.GeneralMeaning(band)) }
                : new Optional<IReadOnlyList<IEmbedField>>(),
            Colour: outcome is { } o ? MoveRoller.ColorFor(o) : RollColor);

        return new BuiltRoll(embed, roll);
    }

    // A roll against a list isn't a move, so show what it landed on instead of hit/miss
    private static Embed BuildPick(IMessageCreate message, DiceMessage request, IReadOnlyDictionary<int, string> options, DiceResult roll)
    {
        var picked = options.TryGetValue(roll.Value, out var name)
            ? $"**{roll.Value}** → **{name}**"
            : $"**{roll.Value}** isn't on the list ({options.Keys.Min()}–{options.Keys.Max()})";

        return new Embed(
            Title: Title(message.Author, request.Label),
            Description: DiceHelper.BuildDiceResult(roll),
            Fields: new List<IEmbedField> { new EmbedField("Picked", picked) },
            Colour: RollColor);
    }

    private BuiltRoll BuildStatRoll(IMessageCreate message, DiceMessage request, Character? character)
    {
        var statType = request.Stat!.Value;
        var stat = character?.Stats.FirstOrDefault(s => s.StatType == statType);

        if (character is null || stat is null)
        {
            return new BuiltRoll(new Embed(
                Title: Title(message.Author, request.Label),
                Description: $"You need a character to roll +{statType.ToString().ToUpper()}. Try using /character create",
                Colour: Color.Red), null);
        }

        var roll = MoveRoller.Roll(diceRoller, stat.Modifier + request.Bonus);
        var rolledWith = $"{statType.ToString().ToUpper()} {Numbers.Signed(stat.Modifier)}"
                         + (request.Bonus != 0 ? $", Bonus {Numbers.Signed(request.Bonus)}" : string.Empty);

        var embed = new Embed(
            Title: Title(message.Author, request.Label, character.Name),
            Description: DiceHelper.BuildDiceResult(roll.Dice),
            Fields: new List<IEmbedField>
            {
                new EmbedField("Rolled with", rolledWith, IsInline: true),
                new EmbedField(MoveRoller.Describe(roll.Outcome), MoveRoller.GeneralMeaning(roll.Outcome)),
            },
            Colour: MoveRoller.ColorFor(roll.Outcome));

        return new BuiltRoll(embed, roll.Dice);
    }

    private static string Title(IUser author, string? label, string? characterName = null)
    {
        var who = characterName ?? author.GlobalName.OrDefault(null) ?? author.Username;
        return label is null ? $"🎲 {who} rolled" : $"🎲 {who}: {label}";
    }
}

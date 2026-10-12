using System.Drawing;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Helpers;
using DungeonWorldBot.Moves;
using DungeonWorldBot.Services;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace DungeonWorldBot.Responder;

/// <summary>
/// Rolls dice for chat messages like ".2d6+1 sneak attack" or ".dex+1 dodge the trap".
/// </summary>
public class DiceRollResponder : IResponder<IMessageCreate>
{
    private static readonly Color RollColour = Color.Fuchsia;

    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly IDiceRoller _diceRoller;
    private readonly ICharacterService _characterService;

    public DiceRollResponder(IDiscordRestChannelAPI channelAPI, IDiceRoller diceRoller, ICharacterService characterService)
    {
        _channelAPI = channelAPI;
        _diceRoller = diceRoller;
        _characterService = characterService;
    }

    public async Task<Result> RespondAsync(IMessageCreate gatewayEvent, CancellationToken ct = default)
    {
        if (gatewayEvent.Author.IsBot.OrDefault(false))
            return Result.FromSuccess();

        // Anything that isn't a roll (e.g. "..." or ".hi") is just a normal message
        var request = DiceMessage.TryParse(gatewayEvent.Content);
        if (request is null)
            return Result.FromSuccess();

        var embed = request.Stat is not null
            ? await BuildStatRollAsync(gatewayEvent, request)
            : BuildDiceRoll(gatewayEvent, request);

        if (embed is null)
            return Result.FromSuccess();

        var reply = await _channelAPI.CreateMessageAsync(
            gatewayEvent.ChannelID,
            embeds: new[] { embed },
            messageReference: new MessageReference(MessageID: gatewayEvent.ID, FailIfNotExists: false),
            allowedMentions: new AllowedMentions(MentionRepliedUser: false),
            ct: ct);

        return reply.IsSuccess ? Result.FromSuccess() : Result.FromError(reply);
    }

    private Embed? BuildDiceRoll(IMessageCreate message, DiceMessage request)
    {
        if (!_diceRoller.TryRoll(request.Expression!, out var roll))
            return null;

        return new Embed(
            Title: Title(message.Author, request.Label),
            Description: DiceHelper.BuildDiceResult(roll),
            Colour: RollColour);
    }

    private async Task<Embed> BuildStatRollAsync(IMessageCreate message, DiceMessage request)
    {
        var statType = request.Stat!.Value;
        var character = await _characterService.GetCharacterFromUserAsync(message.Author.ID);
        var stat = character?.Stats.FirstOrDefault(s => s.StatType == statType);

        if (character is null || stat is null)
        {
            return new Embed(
                Title: Title(message.Author, request.Label),
                Description: $"You need a character to roll +{statType.ToString().ToUpper()}. Try using /character create",
                Colour: Color.Red);
        }

        var roll = MoveRoller.Roll(_diceRoller, stat.Modifier + request.Bonus);
        var rolledWith = $"{statType.ToString().ToUpper()} {Signed(stat.Modifier)}"
                         + (request.Bonus != 0 ? $", Bonus {Signed(request.Bonus)}" : string.Empty);

        return new Embed(
            Title: Title(message.Author, request.Label, character.Name),
            Description: DiceHelper.BuildDiceResult(roll.Dice),
            Fields: new List<IEmbedField>
            {
                new EmbedField("Rolled with", rolledWith, IsInline: true),
                new EmbedField("Result", MoveRoller.Describe(roll.Outcome), IsInline: true),
            },
            Colour: roll.Outcome switch
            {
                MoveOutcome.StrongHit => Color.Green,
                MoveOutcome.WeakHit => Color.Orange,
                _ => Color.Red,
            });
    }

    private static string Title(IUser author, string? label, string? characterName = null)
    {
        var who = characterName ?? author.GlobalName.OrDefault(null) ?? author.Username;
        return label is null ? $"🎲 {who} rolled" : $"🎲 {who}: {label}";
    }

    private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
}

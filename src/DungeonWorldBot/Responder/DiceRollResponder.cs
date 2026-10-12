using System.Drawing;
using DiceNotation;
using DungeonWorldBot.Helpers;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway.Responders;
using Remora.Results;

namespace DungeonWorldBot.Responder;

public class DiceRollResponder: IResponder<IMessageCreate>
{
    private readonly IDiscordRestChannelAPI _channelAPI;
    private readonly IDiceParser _diceParser;
    
    public DiceRollResponder(IDiscordRestChannelAPI channelAPI, IDiceParser diceParser)
    {
        _channelAPI = channelAPI;
        _diceParser = diceParser;
    }
    
    public async Task<Result> RespondAsync(IMessageCreate gatewayEvent, CancellationToken ct = default)
    {
        if (gatewayEvent.Author.IsBot.OrDefault(false) || !gatewayEvent.Content.StartsWith('.'))
            return Result.FromSuccess();

        // Anything that isn't valid dice notation (e.g. "..." or ".hi") is just a normal message
        if (!DiceHelper.TryRoll(_diceParser, gatewayEvent.Content[1..], out var roll))
            return Result.FromSuccess();

        var rollText = DiceHelper.BuildDiceResult(roll);
        
        var embed = new Embed(
            Title: "🎲 Roll(s)", 
            Description: rollText,
            Colour: Color.Fuchsia
        );

        var replyResult = await _channelAPI.CreateMessageAsync(gatewayEvent.ChannelID, embeds: new[] { embed }, ct: ct);

        return !replyResult.IsSuccess
            ? Result.FromError(replyResult)
            : Result.FromSuccess();
    }
}
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Gateway.Results;
using Remora.Results;

var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var botToken = "Bot_Token";

var services = new ServiceCollection()
    .AddDiscordGateway(_ => botToken)
    .Configure<DiscordGatewayClientOptions>(g => g.Intents |= GatewayIntents.MessageContents)
    .AddResponder<PingPongResponder>()
    .BuildServiceProvider();

var gatewayClient = services.GetRequiredService<DiscordGatewayClient>();
var log = services.GetRequiredService<ILogger<Program>>();

var runResult = await gatewayClient.RunAsync(cancellationSource.Token);

if (!runResult.IsSuccess)
{
    switch (runResult.Error)
    {
        case ExceptionError exe:
        {
            log.LogError
            (
                exe.Exception,
                "Exception during gateway connection: {ExceptionMessage}",
                exe.Message
            );

            break;
        }
        case GatewayWebSocketError:
        case GatewayDiscordError:
        {
            log.LogError("Gateway error: {Message}", runResult.Error.Message);
            break;
        }
        default:
        {
            log.LogError("Unknown error: {Message}", runResult.Error.Message);
            break;
        }
    }
}

Console.WriteLine("Bye bye");

public class PingPongResponder : IResponder<IMessageCreate>
{
    private readonly IDiscordRestChannelAPI _channelAPI;

    public PingPongResponder(IDiscordRestChannelAPI channelAPI)
    {
        _channelAPI = channelAPI;
    }

    public async Task<Result> RespondAsync
    (
        IMessageCreate gatewayEvent,
        CancellationToken ct = default
    )
    {
        if (gatewayEvent.Content != "!ping")
        {
            return Result.FromSuccess();
        }

        var embed = new Embed(Description: "Pong!", Colour: Color.LawnGreen);
        return (Result)await _channelAPI.CreateMessageAsync
        (
            gatewayEvent.ChannelID,
            embeds: new[] { embed },
            ct: ct
        );
    }
}
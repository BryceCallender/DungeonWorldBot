using DDBot.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Remora.Commands.Extensions;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Results;
using Remora.Results;

var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (sender, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var botToken = "bot_token";

var services = new ServiceCollection()
    .AddDiscordGateway(_ => botToken)
    .Configure<DiscordGatewayClientOptions>(g => g.Intents |= GatewayIntents.MessageContents)
    .AddDiscordCommands()
    .AddCommandTree()
    .WithCommandGroup<DiceRollCommand>()
    .Finish()
    .AddLogging(builder =>
    {
        builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
    })
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
using DungeonWorldBot.Data;
using DungeonWorldBot.Extensions;
using DungeonWorldBot.Services;
using DungeonWorldBot.Services.Implementation;
using DungeonWorldBot.Services.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway;
using Remora.Discord.Hosting.Extensions;

namespace DungeonWorldBot;

public class Program
{ 
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args)
            .UseConsoleLifetime()
            .Build();

        var services = host.Services;
        var log = services.GetRequiredService<ILogger<Program>>();
        
        var slashService = services.GetRequiredService<SlashService>();

        var checkSlashSupport = slashService.SupportsSlashCommands();
        if (!checkSlashSupport.IsSuccess)
        {
            log.LogWarning
            (
                "The registered commands of the bot don't support slash commands: {Reason}",
                checkSlashSupport.Error?.Message
            );
        }
        else
        {
            var updateSlash = await slashService.UpdateSlashCommandsAsync();
            if (!updateSlash.IsSuccess)
            {
                log.LogWarning("Failed to update slash commands: {Reason}", updateSlash.Error?.Message);
            }
        }

        await host.RunAsync();
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .AddDiscordService
        (
            services =>
            {
                var configuration = services.GetRequiredService<IConfiguration>();

                return configuration.GetValue<string?>("REMORA_BOT_TOKEN") ??
                       throw new InvalidOperationException
                       (
                           "No bot token has been provided. Set the REMORA_BOT_TOKEN environment variable to a " +
                           "valid token."
                       );
            }
        )
        .ConfigureServices
        (
            (context, services) =>
            {
                
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();
                
                services.Configure<DiscordGatewayClientOptions>(g =>
                {
                    g.Intents |= GatewayIntents.MessageContents;
                    g.Intents |= GatewayIntents.DirectMessages;
                });
                services
                    .AddSqlite<DungeonWorldContext>(config.GetConnectionString("DungeonWorldConnectionString"))
                    .AddScoped<ICharacterService, CharacterService>()
                    .AddSingleton<InteractiveMessageTracker>()
                    .AddScoped<InteractivityService>()
                    .AddRemoraServices();
            }
        )
        .ConfigureLogging
        (
            c => c
                .AddConsole()
                .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
        );
}
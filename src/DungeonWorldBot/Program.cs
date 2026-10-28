using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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

        // The dashboard reads this file while the bot writes it. WAL lets those reads run alongside a write instead of
        // locking the bot out. The mode is saved in the file, so this only changes anything the first time.
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DungeonWorldContext>();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

            // EF builds its model and compiles each query shape on first use, which would otherwise slow down
            // whichever command runs first
            await scope.ServiceProvider.GetRequiredService<ICharacterService>().GetCharactersAsync();
        }

        var slashService = services.GetRequiredService<SlashService>();

        var updateSlash = await slashService.UpdateSlashCommandsAsync();
        if (!updateSlash.IsSuccess)
        {
            log.LogWarning("Failed to update slash commands: {Reason}", updateSlash.Error?.Message);
        }

        await host.RunAsync();
    }
    
    private static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        // The default builder only reads user secrets in Development, and the bot runs as Production. Where none are
        // set (like on the Pi), the token comes from appsettings.json or the environment as before.
        .ConfigureAppConfiguration(c => c.AddUserSecrets<Program>(optional: true))
        .AddDiscordService
        (
            services => services.GetRequiredService<IOptions<BotSettings>>().Value.BotToken!)
        .ConfigureServices
        (
            (context, services) =>
            {
                services.Configure<DiscordGatewayClientOptions>(g =>
                {
                    g.Intents |= GatewayIntents.MessageContent;
                    g.Intents |= GatewayIntents.DirectMessages;
                });
                services.AddBotSettings()
                    .Validate
                    (
                        s => !string.IsNullOrWhiteSpace(s.BotToken),
                        "No bot token has been provided. Set the REMORA_BOT_TOKEN environment variable to a valid token."
                    )
                    .ValidateOnStart();
                services
                    .AddSqlite<DungeonWorldContext>(context.Configuration.GetConnectionString("DungeonWorldConnectionString"))
                    .AddSqlite<CanvasContext>($"Data Source={CanvasDatabasePath(context)};Mode=ReadOnly")
                    .AddAttributedServices(typeof(Program).Assembly)
                    .AddRemoraServices()
                    .AddHostedService<EncounterAnnouncer>()
                    .AddHostedService<MoveRequestWorker>();
            }
        )
        .ConfigureLogging
        (
            c => c
                .AddConsole()
                .AddFilter("System.Net.Http.HttpClient.*.LogicalHandler", LogLevel.Warning)
                .AddFilter("System.Net.Http.HttpClient.*.ClientHandler", LogLevel.Warning)
                .AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning)
        );

    /// <summary>
    /// The dashboard's canvases.db, which the bot only reads for its encounter scenes. Set CanvasDatabasePath if it's
    /// somewhere other than beside the dashboard.
    /// </summary>
    private static string CanvasDatabasePath(HostBuilderContext context) => Path.GetFullPath(
        context.Configuration.GetValue<string?>("CanvasDatabasePath")
        ?? Path.Combine(context.HostingEnvironment.ContentRootPath, "..", "DungeonWorldBot.Dashboard", "canvases.db"));
}

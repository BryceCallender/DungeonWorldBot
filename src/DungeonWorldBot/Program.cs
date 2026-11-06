using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Infrastructure.Telemetry;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway;
using Remora.Discord.Hosting.Extensions;
using Remora.Rest.Core;
using Remora.Results;

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

        await RegisterSlashCommandsAsync(services, log);

        await host.RunAsync();
    }

    /// <summary>
    /// Tells Discord about the slash commands: globally, or only on the CommandServerId server when it's set. Global
    /// ones are cleared then, or every command would show up twice there.
    /// </summary>
    private static async Task RegisterSlashCommandsAsync(IServiceProvider services, ILogger log)
    {
        Snowflake? server = services.GetRequiredService<IOptions<BotSettings>>().Value.CommandServerID is { } id ? new Snowflake(id) : null;

        var updated = await services.GetRequiredService<SlashService>().UpdateSlashCommandsAsync(server);
        if (!updated.IsSuccess)
        {
            log.LogWarning("Failed to update slash commands: {Reason}", updated.Error?.Message);
            return;
        }

        if (server is null)
            return;

        var application = await services.GetRequiredService<IDiscordRestOAuth2API>().GetCurrentBotApplicationInformationAsync();
        var cleared = application.IsSuccess
            ? (Result)await services.GetRequiredService<IDiscordRestApplicationAPI>().BulkOverwriteGlobalApplicationCommandsAsync(application.Entity.ID, [])
            : (Result)application;
        if (!cleared.IsSuccess)
            log.LogWarning("Failed to clear the global slash commands: {Reason}", cleared.Error?.Message);
    }
    
    internal static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        // Checked in every environment, not only Development: a service that can't be built, or a singleton holding on to a
        // scoped one, stops the bot at startup rather than misbehaving partway through a command
        .UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        })
        // The default builder only reads user secrets in Development, and the bot runs as Production. Where none are
        // set (like on the Pi), the token comes from appsettings.json or the environment as before.
        // The environment and command line are read again after them, so either can still override a secret
        .ConfigureAppConfiguration(c => c.AddUserSecrets<Program>(optional: true).AddEnvironmentVariables().AddCommandLine(args))
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
                    .AddCoreServices()
                    .AddAttributedServices(typeof(Program).Assembly)
                    .AddRemoraServices()
                    .AddHostedService<EncounterAnnouncer>()
                    .AddHostedService<MoveRequestWorker>();
                AddTelemetry(context, services);
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
    /// What the bot's been doing: logs, traces and metrics over OpenTelemetry when OTEL_EXPORTER_OTLP_ENDPOINT is set,
    /// and a status file beside its database for the dashboard's health page either way.
    /// </summary>
    private static void AddTelemetry(HostBuilderContext context, IServiceCollection services)
    {
        services
            .AddSanaSolemTelemetry(context.Configuration, "sana-solem-bot")
            .AddSingleton<CommandTally>()
            .AddScoped<CommandTelemetry.Timing>()
            .AddPreExecutionEvent<CommandTelemetry.Before>()
            .AddPostExecutionEvent<CommandTelemetry.After>();

        var databasePath = new SqliteConnectionStringBuilder(context.Configuration.GetConnectionString("DungeonWorldConnectionString")).DataSource;
        if (!string.IsNullOrEmpty(databasePath))
            services.AddHostedService(provider => ActivatorUtilities.CreateInstance<BotStatusWriter>(provider, Path.GetFullPath(databasePath)));
    }

    /// <summary>
    /// The dashboard's canvases.db, which the bot only reads for its encounter scenes. Set CanvasDatabasePath if it's
    /// somewhere other than beside the dashboard.
    /// </summary>
    private static string CanvasDatabasePath(HostBuilderContext context) => Path.GetFullPath(
        context.Configuration.GetValue<string?>("CanvasDatabasePath")
        ?? Path.Combine(context.HostingEnvironment.ContentRootPath, "..", "DungeonWorldBot.Dashboard", "canvases.db"));
}

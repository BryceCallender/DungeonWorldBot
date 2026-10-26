using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Dashboard.Components;
using DungeonWorldBot.Dashboard.Encounters;
using DungeonWorldBot.Dashboard.Live;
using DungeonWorldBot.Dashboard.Npcs;
using DungeonWorldBot.Dashboard.Party;
using DungeonWorldBot.Dashboard.Rolls;
using DungeonWorldBot.Dashboard.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The Discord sign-in secret lives in user secrets (dotnet user-secrets set ...), which are otherwise only read in
// Development, and the dashboard is shared in Production
builder.Configuration.AddUserSecrets<Program>(optional: true);

// Find the build's compressed assets when run with `dotnet run` outside Development (as when sharing), or they're served empty
builder.WebHost.UseStaticWebAssets();

// The bot's database, opened read-only so the dashboard can't change the game by accident. Only three things write to
// it: CharacterJournal, for players' backstories, notes and goals, EncounterDesk, for the DM running fights and keeping
// enemies and saved monsters, and NpcDesk, for the DM looking after the party's NPCs. Set DatabasePath to read another copy.
var databasePath = Path.GetFullPath(
    builder.Configuration.GetValue<string?>("DatabasePath")
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "DungeonWorldBot", "SanaSolem.db"));

builder.Services.AddSqlite<DungeonWorldContext>($"Data Source={databasePath};Mode=ReadOnly");

// The bot's own services, used only for reading
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<ILevelingService, LevelingService>();
builder.Services.AddScoped<IRollStatsService, RollStatsService>();
builder.Services.AddScoped<INpcService, NpcService>();
builder.Services.AddScoped<IEncounterService, EncounterService>();
builder.Services.AddScoped<IFightLogService, FightLogService>();
builder.Services.AddScoped<ISpotlightService, SpotlightService>();
builder.Services.AddScoped<ITimelineService, TimelineService>();
builder.Services.AddScoped<PartyReader>();
builder.Services.AddScoped<RollReader>();
var writableDatabase = new DbContextOptionsBuilder<DungeonWorldContext>()
    .UseSqlite($"Data Source={databasePath};Mode=ReadWrite")
    .Options;
builder.Services.AddSingleton(new CharacterJournal(writableDatabase));
builder.Services.AddSingleton(new EncounterDesk(writableDatabase));
builder.Services.AddSingleton(new NpcDesk(writableDatabase));

var settings = DashboardSettings.From(builder.Configuration);
builder.Services.AddSingleton(settings);

var signInEnabled = builder.AddDiscordSignIn();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(services =>
    Viewer.From(services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User, signInEnabled, settings));

builder.Services.AddRazorComponents();

// Canvases are the dashboard's own, so they live in a database it can write. Set CanvasDatabasePath to keep it elsewhere.
var canvasDatabasePath = Path.GetFullPath(
    builder.Configuration.GetValue<string?>("CanvasDatabasePath")
    ?? Path.Combine(builder.Environment.ContentRootPath, "canvases.db"));
builder.Services.AddSqlite<CanvasContext>($"Data Source={canvasDatabasePath}");
builder.Services.AddScoped<CanvasStore>();

// Pages refresh themselves when the bot writes to its database, or anyone changes a canvas
builder.Services.AddSingleton(services =>
    new DatabaseWatcher([databasePath, canvasDatabasePath], services.GetRequiredService<ILogger<DatabaseWatcher>>()));
builder.Services.AddHostedService(services => services.GetRequiredService<DatabaseWatcher>());

builder.Services.AddScoped<PortraitStore>();

// Canvases are shared live between everyone who has one open
// The default 32 KB is less than one long freehand stroke; CanvasStore.MaxItemLength sets the real limit per item
builder.Services.AddSignalR(o => o.MaximumReceiveMessageSize = 512 * 1024)
    // Item types go back and forth as "stroke", "token" and "text" rather than numbers
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddSingleton<CanvasPresence>();

// Moves made from tokens on a fight's board are rolled by the bot, and boards hear how they went
builder.Services.AddScoped<FightMoves>();
builder.Services.AddSingleton<MoveResults>();
builder.Services.AddHostedService(services => services.GetRequiredService<MoveResults>());

var app = builder.Build();

if (!File.Exists(databasePath))
    app.Logger.LogWarning("No database at {Path}. Set DatabasePath to the bot's SanaSolem.db.", databasePath);
else
    app.Logger.LogInformation("Reading {Path}", databasePath);

// Creates the canvas database, or brings it up to date, before anyone opens a canvas
using (var scope = app.Services.CreateScope())
{
    var canvasDb = scope.ServiceProvider.GetRequiredService<CanvasContext>();
    canvasDb.Database.Migrate();
}

if (!signInEnabled)
    app.Logger.LogWarning("Discord sign-in is off, so anyone who can reach the dashboard can see it. Set Discord:ClientId and Discord:ClientSecret to turn it on.");
else if (app.Configuration["Discord:GuildId"] is null)
    app.Logger.LogWarning("Discord:GuildId isn't set, so any Discord account can sign in, not just the server's members.");

// Behind a tunnel the dashboard sees plain http from localhost. Trust the tunnel's word that the visitor used https,
// so Discord sends people back to the right address.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/error", createScopeForErrors: true);

if (signInEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();
app.MapStaticAssets();

var pages = app.MapRazorComponents<App>();
var live = app.MapLiveUpdates();
var canvases = app.MapHub<CanvasHub>("/hubs/canvas");
var canvasForms = app.MapCanvasEndpoints();
var journal = app.MapCharacterJournalEndpoints();
var portraits = app.MapPortraitEndpoints();
var encounters = app.MapEncounterEndpoints();
var enemies = app.MapEnemyEndpoints();
var npcs = app.MapNpcEndpoints();
var rollFeed = app.MapRollFeedEndpoints();

if (signInEnabled)
{
    app.MapDiscordSignIn();
    pages.RequireAuthorization(DiscordSignIn.Policy);
    live.RequireAuthorization(DiscordSignIn.Policy);
    canvases.RequireAuthorization(DiscordSignIn.Policy);
    canvasForms.RequireAuthorization(DiscordSignIn.Policy);
    journal.RequireAuthorization(DiscordSignIn.Policy);
    portraits.RequireAuthorization(DiscordSignIn.Policy);
    encounters.RequireAuthorization(DiscordSignIn.Policy);
    enemies.RequireAuthorization(DiscordSignIn.Policy);
    npcs.RequireAuthorization(DiscordSignIn.Policy);
    rollFeed.RequireAuthorization(DiscordSignIn.Policy);
}

app.Run();

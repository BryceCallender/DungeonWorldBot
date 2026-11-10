using DungeonWorldBot.Dashboard;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Admin;
using DungeonWorldBot.Dashboard.Services.Auth;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Dashboard.Services.Health;
using DungeonWorldBot.Dashboard.Services.Live;
using DungeonWorldBot.Dashboard.Services.Npcs;
using DungeonWorldBot.Dashboard.Services.Party;
using DungeonWorldBot.Dashboard.Services.Rolls;
using DungeonWorldBot.Dashboard.Services.Scenes;
using DungeonWorldBot.Dashboard.Services.Sessions;
using DungeonWorldBot.Dashboard.Services.Shared;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// The Discord sign-in secret lives in user secrets (dotnet user-secrets set ...), which are otherwise only read in
// Development, and the dashboard is shared in Production. The environment and command line are read again after them,
// so either can still override a secret, as they can everything else.
builder.Configuration
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Find the build's compressed assets when run with `dotnet run` outside Development (as when sharing), or they're served empty
builder.WebHost.UseStaticWebAssets();

var databases = DashboardDatabases.From(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(DashboardSettings.From(builder.Configuration));

var signInEnabled = builder.AddDiscordSignIn();
builder.Services.AddViewer(signInEnabled);

builder.Services.AddRazorComponents();
builder.Services
    .AddBotServices(databases)
    .AddLiveUpdates(databases)
    .AddParty()
    .AddNpcs()
    .AddEncounters()
    .AddScenes(databases)
    .AddRolls()
    .AddSessions()
    .AddAdmin(databases)
    .AddHealth(databases, builder.Configuration);

var app = builder.Build();

if (!File.Exists(databases.GamePath))
    app.Logger.LogWarning("No database at {Path}. Set DatabasePath to the bot's SanaSolem.db.", databases.GamePath);
else
    app.Logger.LogInformation("Reading {Path}", databases.GamePath);

app.UpdateCanvasDatabase(databases);

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

// An address that isn't a page gets the not-found page rather than a blank one. The API and hubs keep their bare 404s,
// which the scripts calling them check for.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/hubs"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

// Routing comes after the line above, so the not-found page it sends a 404 to is found again; left to itself, the app
// routes once, first thing, before anything else gets a look in
app.UseRouting();

if (signInEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();
app.MapStaticAssets();

// Everything but signing in is for the server's members. Anything mapped here is covered, so a new endpoint can't be
// left open by accident.
IEndpointConventionBuilder[] membersOnly =
[
    app.MapRazorComponents<App>(),
    app.MapLiveUpdates(),
    app.MapHub<CanvasHub>("/hubs/canvas"),
    app.MapCanvasEndpoints(),
    app.MapMapEndpoints(),
    app.MapCharacterJournalEndpoints(),
    app.MapPortraitEndpoints(),
    app.MapEncounterEndpoints(),
    app.MapEnemyEndpoints(),
    app.MapNpcEndpoints(),
    app.MapRollFeedEndpoints(),
    app.MapAdminEndpoints(),
];

if (signInEnabled)
{
    app.MapDiscordSignIn();
    foreach (var endpoints in membersOnly)
        endpoints.RequireAuthorization(DiscordSignIn.Policy);
}

app.Run();

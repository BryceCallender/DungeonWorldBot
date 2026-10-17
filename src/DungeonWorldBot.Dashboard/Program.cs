using DungeonWorldBot.Dashboard.Components;
using DungeonWorldBot.Dashboard.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// The Discord sign-in secret lives in user secrets (dotnet user-secrets set ...), which are otherwise only read in
// Development, and the dashboard is shared in Production
builder.Configuration.AddUserSecrets<Program>(optional: true);

// Find the build's compressed assets when run with `dotnet run` outside Development (as when sharing), or they're served empty
builder.WebHost.UseStaticWebAssets();

// The bot's database, opened read-only so the dashboard can never change the game. Set DatabasePath to read another copy.
var databasePath = Path.GetFullPath(
    builder.Configuration.GetValue<string?>("DatabasePath")
    ?? Path.Combine(builder.Environment.ContentRootPath, "..", "DungeonWorldBot", "SanaSolem.db"));

builder.Services.AddSqlite<DungeonWorldContext>($"Data Source={databasePath};Mode=ReadOnly");

// The bot's own services, used only for reading
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<ILevelingService, LevelingService>();
builder.Services.AddScoped<IRollStatsService, RollStatsService>();
builder.Services.AddScoped<PartyReader>();
builder.Services.AddScoped<RollReader>();

var settings = DashboardSettings.From(builder.Configuration);
builder.Services.AddSingleton(settings);

// Pages refresh themselves when the bot writes to the database
builder.Services.AddSingleton(services =>
    new DatabaseWatcher(databasePath, services.GetRequiredService<ILogger<DatabaseWatcher>>()));
builder.Services.AddHostedService(services => services.GetRequiredService<DatabaseWatcher>());

var signInEnabled = builder.AddDiscordSignIn();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped(services =>
    Viewer.From(services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User, signInEnabled, settings));

builder.Services.AddRazorComponents();

var app = builder.Build();

if (!File.Exists(databasePath))
    app.Logger.LogWarning("No database at {Path}. Set DatabasePath to the bot's SanaSolem.db.", databasePath);
else
    app.Logger.LogInformation("Reading {Path}", databasePath);

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

if (signInEnabled)
{
    app.MapDiscordSignIn();
    pages.RequireAuthorization(DiscordSignIn.Policy);
    live.RequireAuthorization(DiscordSignIn.Policy);
}

app.Run();

using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace DungeonWorldBot.Dashboard.Auth;

/// <summary>
/// "Sign in with Discord", limited to members of the campaign's server.
/// </summary>
/// <remarks>
/// On only when Discord:ClientId and Discord:ClientSecret are set (from the bot's application in the Discord developer
/// portal). Discord:GuildId limits it to that server's members. Without them the dashboard is open to anyone who can
/// reach it, as before.
/// </remarks>
public static class DiscordSignIn
{
    public const string Scheme = "Discord";
    public const string Policy = "ServerMember";

    public const string MemberClaim = "urn:sanasolem:member";
    public const string AvatarClaim = "urn:discord:avatar";
    public const string DisplayNameClaim = "urn:discord:display_name";

    private const string Api = "https://discord.com/api";

    /// <returns>Whether sign-in is on.</returns>
    public static bool AddDiscordSignIn(this WebApplicationBuilder builder)
    {
        var discord = builder.Configuration.GetSection("Discord");

        // Lets local testing skip sign-in without touching the secrets (Discord:SignIn=false)
        if (!discord.GetValue("SignIn", true))
            return false;

        var clientId = discord["ClientId"];
        var clientSecret = discord["ClientSecret"];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            return false;

        var guildId = discord.GetValue<ulong?>("GuildId");

        builder.Services
            .AddAuthentication(options =>
            {
                // Signed-out visitors go to /login, which sends them on to Discord
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "SanaSolem.SignIn";
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/denied";
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;

                // The live stream and canvas hub can't follow a redirect to Discord, so they get a plain 401
                options.Events.OnRedirectToLogin = context =>
                {
                    if (NoRedirects(context.Request.Path))
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    // A pasted link still gets its card in Discord
                    else if (LinkPreview.IsFetcher(context.Request))
                        return LinkPreview.WriteCardAsync(context.HttpContext);
                    else
                        context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (NoRedirects(context.Request.Path))
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    else
                        context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOAuth(Scheme, options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.CallbackPath = "/signin-discord";
                options.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
                options.TokenEndpoint = $"{Api}/oauth2/token";
                options.UserInformationEndpoint = $"{Api}/users/@me";
                options.UsePkce = true;

                options.Scope.Add("identify");
                // Lets the dashboard ask whether they're in the server, and for their nickname there
                if (guildId is not null)
                    options.Scope.Add("guilds.members.read");

                // Discord sends people back with a top-level redirect, which Lax cookies survive. The defaults (None,
                // https only) break signing in over plain http on localhost; behind a tunnel it's https anyway.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                options.ClaimActions.MapJsonKey(AvatarClaim, "avatar");
                options.ClaimActions.MapCustomJson(DisplayNameClaim, user =>
                    String(user, "global_name") ?? String(user, "username"));

                options.Events.OnCreatingTicket = async context =>
                {
                    using var user = await GetAsync(context, context.Options.UserInformationEndpoint)
                        ?? throw new InvalidOperationException("Discord didn't say who signed in.");
                    context.RunClaimActions(user.RootElement);

                    var identity = context.Identity!;
                    if (guildId is null)
                    {
                        identity.AddClaim(new Claim(MemberClaim, "anyone"));
                        return;
                    }

                    // Not found when they aren't in the server, which leaves them without the member claim
                    using var member = await GetAsync(context, $"{Api}/users/@me/guilds/{guildId}/member");
                    if (member is null)
                        return;

                    identity.AddClaim(new Claim(MemberClaim, guildId.Value.ToString()));

                    // Their server nickname is the name the table knows them by
                    if (String(member.RootElement, "nick") is { } nick && identity.FindFirst(DisplayNameClaim) is { } display)
                    {
                        identity.RemoveClaim(display);
                        identity.AddClaim(new Claim(DisplayNameClaim, nick));
                    }
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policy, policy => policy.RequireAuthenticatedUser().RequireClaim(MemberClaim));

        return true;
    }

    public static void MapDiscordSignIn(this WebApplication app)
    {
        app.MapGet("/login", (string? returnUrl) =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = LocalOnly(returnUrl) }, [Scheme]));

        app.MapGet("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        });
    }

    private static bool NoRedirects(PathString path) =>
        path.StartsWithSegments("/live") || path.StartsWithSegments("/hubs");

    // Only ever send people back into the dashboard, never to another site
    private static string LocalOnly(string? returnUrl) =>
        returnUrl is { Length: > 0 } && returnUrl[0] == '/' && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
            ? returnUrl
            : "/";

    private static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <returns>The response, or null if Discord said no (like "not a member").</returns>
    private static async Task<JsonDocument?> GetAsync(OAuthCreatingTicketContext context, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        request.Headers.UserAgent.ParseAdd("SanaSolemDashboard/1.0");

        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
            return null;

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
    }
}

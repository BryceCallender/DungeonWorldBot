using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace DungeonWorldBot.Dashboard.Services.Auth;

/// <summary>
/// For trying the dashboard locally as a particular player, without Discord: in Development only, with DevSignInAs set
/// to their Discord ID (DevSignInName for the name shown, DevSignInAdmin=true for the Admin role), every request is
/// signed in as them. With it, pages show what that player would see, which sign-in being off doesn't.
/// </summary>
/// <remarks>
/// Never on outside Development, whatever's set, so the shared dashboard can't be opened up by a stray setting.
/// </remarks>
public static class DevSignIn
{
    private const string Scheme = "DevSignIn";

    /// <returns>Whether it's on.</returns>
    public static bool AddDevSignIn(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment() || builder.Configuration.GetValue<ulong?>("DevSignInAs") is not { } id)
            return false;

        var name = builder.Configuration["DevSignInName"] ?? $"Player {id}";
        var admin = builder.Configuration.GetValue("DevSignInAdmin", false);
        builder.Services
            .AddAuthentication(Scheme)
            .AddScheme<AuthenticationSchemeOptions, Handler>(Scheme, _ => { });
        builder.Services.AddSingleton(new SignedInAs(id, name, admin));
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(DiscordSignIn.Policy, policy => policy.RequireAuthenticatedUser().RequireClaim(DiscordSignIn.MemberClaim));
        return true;
    }

    private record SignedInAs(ulong ID, string Name, bool Admin);

    private class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, SignedInAs who)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            List<Claim> claims =
            [
                new(ClaimTypes.NameIdentifier, who.ID.ToString()),
                new(ClaimTypes.Name, who.Name),
                new(DiscordSignIn.DisplayNameClaim, who.Name),
                new(DiscordSignIn.MemberClaim, "dev"),
            ];
            if (who.Admin)
                claims.Add(new Claim(DiscordSignIn.AdminClaim, "dev"));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, DevSignIn.Scheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, DevSignIn.Scheme)));
        }
    }
}

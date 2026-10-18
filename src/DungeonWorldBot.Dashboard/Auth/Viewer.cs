using System.Security.Claims;
using DungeonWorldBot.Dashboard.Shared;

namespace DungeonWorldBot.Dashboard.Auth;

/// <summary>
/// Who's looking at the page. Without Discord sign-in, nobody in particular.
/// </summary>
/// <param name="ID">Their Discord ID, which is also their character's ID.</param>
public sealed record Viewer(bool SignInEnabled, ulong? ID, string? Name, string? AvatarUrl, bool IsDungeonMaster)
{
    public bool SignedIn => ID is not null;

    public bool Is(ulong id) => ID == id;

    public bool Is(string id) => ID?.ToString() == id;

    public static Viewer From(ClaimsPrincipal? user, bool signInEnabled, DashboardSettings settings)
    {
        if (user?.Identity?.IsAuthenticated != true || !ulong.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return new Viewer(signInEnabled, null, null, null, false);

        var avatar = user.FindFirstValue(DiscordSignIn.AvatarClaim);
        return new Viewer(
            signInEnabled,
            id,
            user.FindFirstValue(DiscordSignIn.DisplayNameClaim) ?? user.FindFirstValue(ClaimTypes.Name),
            string.IsNullOrEmpty(avatar)
                ? $"https://cdn.discordapp.com/embed/avatars/{(id >> 22) % 6}.png"
                : $"https://cdn.discordapp.com/avatars/{id}/{avatar}.png?size=64",
            id == settings.DungeonMasterID);
    }
}

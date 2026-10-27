using System.Security.Claims;
using DungeonWorldBot.Dashboard.Models.Scenes;
using DungeonWorldBot.Dashboard.Services.Auth;

namespace DungeonWorldBot.Dashboard.Models.Auth;

/// <summary>
/// Who's looking at the page. Without Discord sign-in, nobody in particular.
/// </summary>
/// <param name="ID">Their Discord ID, which is also their character's ID.</param>
public record Viewer(bool SignInEnabled, ulong? ID, string? Name, string? AvatarUrl, bool IsDungeonMaster)
{
    public bool SignedIn => ID is not null;

    public bool Is(ulong id) => ID == id;

    public bool Is(string id) => ID?.ToString() == id;

    /// <summary>
    /// Whether they run the game: the DM, or anyone when sign-in is off. They run fights, look after NPCs, enemies and
    /// saved monsters, and can change anyone's picture.
    /// </summary>
    public bool CanRunGame => !SignedIn || IsDungeonMaster;

    /// <summary>
    /// Whether they can put a scene away, bring it back or delete it: whoever runs the game, or whoever made it, so
    /// nobody clears the board in the middle of a fight.
    /// </summary>
    public bool CanManage(Canvas canvas) => CanRunGame || Is(canvas.CreatedBy ?? "");

    public static Viewer From(ClaimsPrincipal? user, bool signInEnabled)
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
            user.HasClaim(c => c.Type == DiscordSignIn.DungeonMasterClaim));
    }
}

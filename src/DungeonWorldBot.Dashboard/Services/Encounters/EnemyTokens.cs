using System.Text.Json;
using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Dashboard.Services.Scenes;
using DungeonWorldBot.Data.Entities;
using Microsoft.AspNetCore.SignalR;

namespace DungeonWorldBot.Dashboard.Services.Encounters;

/// <summary>
/// Tokens for enemies added to a fight from its scene, and for the party when it starts, put on the board in open spots
/// near the middle and sent to everyone looking at it.
/// </summary>
/// <remarks>
/// Built the way the board builds a token: a group holding a colored circle, named for who it is. With a picture the
/// circle carries its address in src, like the board's image tokens; without one the board adds the initials itself
/// when it loads it, so "Goblin 2" shows as G2.
/// </remarks>
public static class EnemyTokens
{
    // The same as the board's: TOKEN_RADIUS in canvas.ts, and the red from its swatches
    private const double Radius = 36;
    private const string EnemyColor = "#D85A30";

    // The board's blue and green, so the party and their NPCs stand apart from the enemies
    private const string PartyColor = "#378ADD";
    private const string NpcColor = "#1D9E75";

    // A token and a little room around it
    private const double Spacing = Radius * 2 + 12;

    // The middle of the board's home view (HOME in canvas.ts)
    private const double CenterX = 800;
    private const double CenterY = 500;

    /// <summary>
    /// Who the tokens are from, as far as the board's records go.
    /// </summary>
    public const string AuthorID = "encounter";

    /// <param name="pictureUrl">A picture to fill each circle with instead of initials, one of the scene's own.</param>
    public static async Task PlaceAsync(
        int canvasID,
        IReadOnlyList<Enemy> enemies,
        CanvasStore store,
        IHubContext<CanvasHub, ICanvasClient> hub,
        string? pictureUrl = null) =>
        await PlaceAsync(canvasID, enemies.Select(e => new NewToken(e.Name, pictureUrl, TokenFighter.Enemy(e.ID))).ToList(), EnemyColor, store, hub);

    /// <summary>
    /// A token for each character and NPC joining the fight, leaving out any already on the board from an earlier one.
    /// </summary>
    /// <param name="characters">Each with their picture, if they have one (see <see cref="Party.PortraitStore"/>).</param>
    public static async Task PlacePartyAsync(
        int canvasID,
        IReadOnlyList<NewToken> characters,
        IReadOnlyList<NewToken> npcs,
        CanvasStore store,
        IHubContext<CanvasHub, ICanvasClient> hub)
    {
        var onBoard = (await store.ItemsAsync(canvasID))
            .Where(i => i.Type == CanvasItemType.Token)
            .Select(i => NameOf(i.Json))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        await PlaceAsync(canvasID, characters.Where(c => !onBoard.Contains(c.Name)).ToList(), PartyColor, store, hub);
        await PlaceAsync(canvasID, npcs.Where(n => !onBoard.Contains(n.Name)).ToList(), NpcColor, store, hub);
    }

    private static async Task PlaceAsync(
        int canvasID,
        IReadOnlyList<NewToken> newTokens,
        string color,
        CanvasStore store,
        IHubContext<CanvasHub, ICanvasClient> hub)
    {
        if (newTokens.Count == 0)
            return;

        var taken = (await store.ItemsAsync(canvasID))
            .Where(i => i.Type == CanvasItemType.Token)
            .Select(i => PositionOf(i.Json))
            .ToList();

        foreach (var (name, pictureUrl, fighter) in newTokens)
        {
            var spot = FreeSpot(taken);
            taken.Add(spot);

            var id = Guid.NewGuid().ToString();
            var token = JsonSerializer.SerializeToElement(new
            {
                attrs = fighter is null
                    ? (object)new { x = spot.X, y = spot.Y, name }
                    : new { x = spot.X, y = spot.Y, name, fighter = fighter.ToString() },
                className = "Group",
                children = new[]
                {
                    new
                    {
                        // The board puts the picture from src into the circle, as it does for its image tokens
                        attrs = pictureUrl is null
                            ? (object)new { radius = Radius, fill = color, stroke = "#ffffff", strokeWidth = 2 }
                            : new { radius = Radius, fill = color, stroke = "#ffffff", strokeWidth = 2, src = pictureUrl },
                        className = "Circle",
                    },
                },
            });

            await store.SaveItemAsync(canvasID, id, CanvasItemType.Token, token, AuthorID);
            await hub.Clients.Group(CanvasHub.Group(canvasID)).ItemAdded(AuthorID, id, CanvasItemType.Token, token);
        }
    }

    /// <summary>
    /// The nearest spot to the middle of the board on a grid a token apart, clear of every token already there.
    /// </summary>
    public static (double X, double Y) FreeSpot(IReadOnlyList<(double X, double Y)> taken)
    {
        const int reach = 8;
        var spots =
            from dx in Enumerable.Range(-reach, reach * 2 + 1)
            from dy in Enumerable.Range(-reach, reach * 2 + 1)
            orderby dx * dx + dy * dy, dy, dx
            select (X: CenterX + dx * Spacing, Y: CenterY + dy * Spacing);

        foreach (var spot in spots)
        {
            if (taken.All(t => Math.Abs(t.X - spot.X) >= Spacing || Math.Abs(t.Y - spot.Y) >= Spacing))
                return spot;
        }

        // A board that full can take one more in the middle
        return (CenterX, CenterY);
    }

    // Konva saves a node as { attrs: { x, y, ... } }, leaving out a 0
    private static (double X, double Y) PositionOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("attrs", out var attrs))
            return (0, 0);

        var x = attrs.TryGetProperty("x", out var xValue) && xValue.TryGetDouble(out var px) ? px : 0;
        var y = attrs.TryGetProperty("y", out var yValue) && yValue.TryGetDouble(out var py) ? py : 0;
        return (x, y);
    }

    // Tokens are named for who they are; ones without a name have none to match
    private static string? NameOf(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("attrs", out var attrs)
               && attrs.TryGetProperty("name", out var name)
               && name.ValueKind == JsonValueKind.String
            ? name.GetString()
            : null;
    }
}

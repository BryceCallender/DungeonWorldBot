using System.Text.Json;
using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Dashboard.Models.Scenes;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Services.Scenes;

/// <summary>
/// What the hub calls in the browser. Each name is the event canvas-live.js listens for.
/// </summary>
public interface ICanvasClient
{
    Task PresenceChanged(IReadOnlyList<CanvasPerson> people);
    Task Cursor(string personID, double x, double y);
    Task View(string personID, double x, double y, double width, double height);
    Task StrokeProgress(string personID, string strokeID, double[] points);
    Task StrokeEnd(string personID, string strokeID, JsonElement stroke);
    Task ItemAdded(string personID, string itemID, CanvasItemType type, JsonElement item);
    Task ItemMoved(string personID, string itemID, double x, double y);
    Task ItemRemoved(string personID, string itemID);

    /// <summary>
    /// Someone made a move from one token at another, and the bot is rolling it.
    /// </summary>
    Task MoveAsked(string personID, string actor, string target);

    Task MoveResolved(MoveResult result);
}

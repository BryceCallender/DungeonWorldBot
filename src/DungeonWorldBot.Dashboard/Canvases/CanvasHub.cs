using System.Text.Json;
using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// Shares a canvas live between everyone who has it open: who's there, where their cursors are, and what they draw.
/// </summary>
/// <remarks>
/// Finished strokes, tokens and text are saved, so someone who opens the canvas later sees them. Cursors and strokes
/// still being drawn are only passed on.
/// </remarks>
public sealed class CanvasHub(CanvasPresence presence, CanvasStore store, DashboardSettings settings) : Hub<ICanvasClient>
{
    private static string Group(int canvas) => $"canvas:{canvas}";

    /// <returns>Who they are, as the others see them, everyone on the canvas, and everything on it.</returns>
    /// <param name="sqid">The canvas's short ID, from <see cref="CanvasIds"/>.</param>
    public async Task<JoinResult> Join(string sqid)
    {
        if (!CanvasIds.TryDecode(sqid, out var canvas) || await store.FindAsync(canvas) is null)
            throw new HubException("There's no such scene.");

        var me = Me();
        var previous = presence.Join(Context.ConnectionId, canvas, me);
        if (previous is not null)
            await LeaveGroup(previous.Value);

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(canvas));
        var people = presence.On(canvas);
        await Clients.OthersInGroup(Group(canvas)).PresenceChanged(people);

        var items = await store.ItemsAsync(canvas);
        return new JoinResult(me, people, items.Select(SavedItem.From).ToList());
    }

    public async Task Leave()
    {
        if (presence.Leave(Context.ConnectionId) is { } canvas)
            await LeaveGroup(canvas);
    }

    public async Task Cursor(double x, double y)
    {
        var (canvas, me) = Current();
        await Clients.OthersInGroup(Group(canvas)).Cursor(me.ID, x, y);
    }

    /// <param name="points">The points added since the last call, as x, y, x, y...</param>
    public async Task StrokeProgress(string strokeID, double[] points)
    {
        var (canvas, me) = Current();
        await Clients.OthersInGroup(Group(canvas)).StrokeProgress(me.ID, strokeID, points);
    }

    /// <param name="stroke">The finished stroke, as the page draws it (like a Konva node's toObject()).</param>
    public async Task StrokeEnd(string strokeID, JsonElement stroke)
    {
        var (canvas, me) = Current();
        await store.SaveItemAsync(canvas, CheckID(strokeID), CanvasItemType.Stroke, Check(stroke), me.ID);
        await Clients.OthersInGroup(Group(canvas)).StrokeEnd(me.ID, strokeID, stroke);
    }

    /// <summary>
    /// Adds a token or some text, or replaces it when it's changed (like a token's HP or a note's words).
    /// </summary>
    public async Task AddItem(string itemID, CanvasItemType type, JsonElement item)
    {
        var (canvas, me) = Current();
        await store.SaveItemAsync(canvas, CheckID(itemID), type, Check(item), me.ID);
        await Clients.OthersInGroup(Group(canvas)).ItemAdded(me.ID, itemID, type, item);
    }

    public async Task MoveItem(string itemID, double x, double y)
    {
        var (canvas, me) = Current();
        if (await store.MoveItemAsync(canvas, CheckID(itemID), x, y))
            await Clients.OthersInGroup(Group(canvas)).ItemMoved(me.ID, itemID, x, y);
    }

    public async Task RemoveItem(string itemID)
    {
        var (canvas, me) = Current();
        await store.RemoveItemAsync(canvas, CheckID(itemID));
        await Clients.OthersInGroup(Group(canvas)).ItemRemoved(me.ID, itemID);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Groups drop the connection on their own, but the others still need to hear they've gone
        if (presence.Leave(Context.ConnectionId) is { } canvas)
            await Clients.Group(Group(canvas)).PresenceChanged(presence.On(canvas));
        await base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveGroup(int canvas)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(canvas));
        await Clients.Group(Group(canvas)).PresenceChanged(presence.On(canvas));
    }

    private (int Canvas, CanvasPerson Me) Current() =>
        presence.Find(Context.ConnectionId) ?? throw new HubException("Join a scene first.");

    private static string CheckID(string id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= CanvasStore.MaxItemIDLength
            ? id
            : throw new HubException("That isn't an item ID.");

    private static JsonElement Check(JsonElement item) =>
        item.ValueKind == JsonValueKind.Object && item.GetRawText().Length <= CanvasStore.MaxItemLength
            ? item
            : throw new HubException("That item is too big, or isn't an object.");

    private CanvasPerson Me()
    {
        // With sign-in on, only signed-in members get this far, so being signed in says whether sign-in is on
        var viewer = Viewer.From(Context.User, Context.User?.Identity?.IsAuthenticated == true, settings);
        return viewer.ID is { } id
            ? new CanvasPerson(id.ToString(), viewer.Name ?? "Someone", viewer.AvatarUrl, viewer.IsDungeonMaster)
            : new CanvasPerson($"guest-{Context.ConnectionId}", "Guest", null, false);
    }
}

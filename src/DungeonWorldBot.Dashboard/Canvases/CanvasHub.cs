using DungeonWorldBot.Data;
using System.Text.Json;
using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Encounters;
using DungeonWorldBot.Dashboard.Shared;
using DungeonWorldBot.Features.Encounters;
using Microsoft.AspNetCore.SignalR;

namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// Shares a canvas live between everyone who has it open: who's there, where their cursors are, and what they draw.
/// </summary>
/// <remarks>
/// Finished strokes, tokens and text are saved, so someone who opens the canvas later sees them. Cursors and strokes
/// still being drawn are only passed on. While a fight is on an encounter scene, only those who can change its board
/// (see <see cref="FightBoard"/>) can do anything but look.
/// </remarks>
public class CanvasHub(
    CanvasPresence presence,
    CanvasStore store,
    DashboardSettings settings,
    IEncounterService encounters,
    FightMoves moves,
    MoveResults results)
    : Hub<ICanvasClient>
{
    /// <summary>
    /// Everyone with this canvas open, for sending them something from outside the hub.
    /// </summary>
    public static string Group(int canvas) => $"canvas:{canvas}";

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

    /// <summary>
    /// The part of the board they're looking at, in board units, for anyone following them. Only passed on, like
    /// cursors, so those watching a fight can be followed too.
    /// </summary>
    public async Task View(double x, double y, double width, double height)
    {
        var (canvas, me) = Current();
        await Clients.OthersInGroup(Group(canvas)).View(me.ID, x, y, width, height);
    }

    /// <param name="points">The points added since the last call, as x, y, x, y...</param>
    public async Task StrokeProgress(string strokeID, double[] points)
    {
        var (canvas, me) = await ChangerAsync();
        await Clients.OthersInGroup(Group(canvas)).StrokeProgress(me.ID, strokeID, points);
    }

    /// <param name="stroke">The finished stroke, as the page draws it (like a Konva node's toObject()).</param>
    public async Task StrokeEnd(string strokeID, JsonElement stroke)
    {
        var (canvas, me) = await ChangerAsync();
        await store.SaveItemAsync(canvas, CheckID(strokeID), CanvasItemType.Stroke, Check(stroke), me.ID);
        await Clients.OthersInGroup(Group(canvas)).StrokeEnd(me.ID, strokeID, stroke);
    }

    /// <summary>
    /// Adds a token or some text, or replaces it when it's changed (like a token's HP or a note's words).
    /// </summary>
    public async Task AddItem(string itemID, CanvasItemType type, JsonElement item)
    {
        var (canvas, me) = await ChangerAsync();
        await store.SaveItemAsync(canvas, CheckID(itemID), type, Check(item), me.ID);
        await Clients.OthersInGroup(Group(canvas)).ItemAdded(me.ID, itemID, type, item);
    }

    public async Task MoveItem(string itemID, double x, double y)
    {
        var (canvas, me) = await ChangerAsync();
        if (await store.MoveItemAsync(canvas, CheckID(itemID), x, y))
            await Clients.OthersInGroup(Group(canvas)).ItemMoved(me.ID, itemID, x, y);
    }

    public async Task RemoveItem(string itemID)
    {
        var (canvas, me) = await ChangerAsync();
        // Enemies' tokens stay put unless the DM takes them off. With sign-in on, only signed-in members get this far,
        // so someone who isn't signed in means sign-in is off, when anyone runs fights.
        var runsFights = me.IsDungeonMaster || Context.User?.Identity?.IsAuthenticated != true;
        if (!runsFights && await store.IsLockedAsync(canvas, CheckID(itemID)))
            throw new HubException("Only the DM can remove enemy tokens from the board.");
        if (await store.IsLockedInPlaceAsync(canvas, itemID))
            throw new HubException("That picture is locked. Right-click it to unlock it first.");
        await store.RemoveItemAsync(canvas, itemID);
        await Clients.OthersInGroup(Group(canvas)).ItemRemoved(me.ID, itemID);
    }

    /// <summary>
    /// The moves you can make from a token, for its right-click menu: your own character's, or an enemy's if you run the fight.
    /// </summary>
    public async Task<IReadOnlyList<BoardMove>> MovesFor(string itemID)
    {
        var (canvas, _) = await ChangerAsync();
        return await moves.MovesForAsync(canvas, ViewerOf(), CheckID(itemID));
    }

    /// <summary>
    /// Makes a move from a token, like Hack and Slash at an enemy or Spout Lore at no one, for the bot to roll. How it
    /// went comes back to everyone as MoveResolved.
    /// </summary>
    public async Task MakeMove(MoveAsk ask)
    {
        var (canvas, me) = await ChangerAsync();
        var asked = await moves.AskAsync(canvas, ViewerOf(), ask with { Actor = CheckID(ask.Actor), Target = ask.Target is null ? null : CheckID(ask.Target) });
        if (asked.Error is { } error)
            throw new HubException(error);

        // A move aimed at no one shows how it went over whoever made it
        var shownOver = ask.Target ?? ask.Actor;
        results.Follow(asked.ID, new WaitingMove(canvas, me.ID, ask.Actor, shownOver, DateTimeOffset.UtcNow));
        await Clients.OthersInGroup(Group(canvas)).MoveAsked(me.ID, ask.Actor, shownOver);
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

    // A drag sends a dozen moves a second, so whether they can change the board is worked out at most this often
    private static readonly TimeSpan RecheckAfter = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Whether this connection could change its board, and when that was worked out.
    /// </summary>
    private record ChangeCheck(int Canvas, bool Allowed, DateTimeOffset At);

    /// <summary>
    /// <see cref="Current"/>, for changing the board: refused for anyone kept out of it while a fight's on.
    /// </summary>
    private async Task<(int Canvas, CanvasPerson Me)> ChangerAsync()
    {
        var current = Current();
        var now = DateTimeOffset.UtcNow;
        if (Context.Items.TryGetValue(nameof(ChangeCheck), out var cached)
            && cached is ChangeCheck check && check.Canvas == current.Canvas && now - check.At < RecheckAfter)
        {
            return check.Allowed ? current : throw new HubException(FightBoard.ViewOnlyMessage);
        }

        var allowed = await store.FindAsync(current.Canvas) is not { } scene
                      || await FightBoard.CanChangeAsync(scene, ViewerOf(), encounters);
        Context.Items[nameof(ChangeCheck)] = new ChangeCheck(current.Canvas, allowed, now);
        return allowed ? current : throw new HubException(FightBoard.ViewOnlyMessage);
    }

    // With sign-in on, only signed-in members get this far, so being signed in says whether sign-in is on
    private Viewer ViewerOf() => Viewer.From(Context.User, Context.User?.Identity?.IsAuthenticated == true, settings);

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
        var viewer = ViewerOf();
        return viewer.ID is { } id
            ? new CanvasPerson(id.ToString(), viewer.Name ?? "Someone", viewer.AvatarUrl, viewer.IsDungeonMaster)
            : new CanvasPerson($"guest-{Context.ConnectionId}", "Guest", null, false);
    }
}

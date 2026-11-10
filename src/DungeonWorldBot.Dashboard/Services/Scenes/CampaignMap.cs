using System.Text.Json;
using DungeonWorldBot.Dashboard.Models.Scenes;
using DungeonWorldBot.Dashboard.Models.Sessions;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.AspNetCore.SignalR;

namespace DungeonWorldBot.Dashboard.Services.Scenes;

/// <summary>
/// The campaign's map: the map scene edited most recently, whose pictures are the map and whose tokens, by name, say
/// where each scene's fights happened, for playing a session's journey back.
/// </summary>
/// <remarks>
/// Places are ordinary tokens named for the place, so ones put there by hand count, they show on the map's board, and
/// moving one there moves it in the journey. Stops the DM adds to a session by hand are kept on the map too, a hidden
/// item a session (like the board's party trail, an ordinary item the board loads and never shows).
/// </remarks>
public class CampaignMap(CanvasStore store, IHubContext<CanvasHub, ICanvasClient> hub)
{
    /// <summary>
    /// What a new map is called, made when the first place is pinned and there's no map scene yet.
    /// </summary>
    public const string NewMapName = "World map";

    // Where boards start (HOME in canvas.ts), for a map with nothing on it yet
    private static readonly MapFrame Home = new(0, 0, 1600, 1000);

    // Room around what's on the map, so the outermost places aren't on its edge
    private const double Margin = 80;

    // A brown that reads on parchment, apart from the party's blue and the enemies' red
    private const double PlaceRadius = 24;
    private const string PlaceColor = "#8A6D3B";

    public async Task<CampaignMapView> GetAsync()
    {
        if (await FindAsync() is not { } map)
            return new CampaignMapView(null, Home, [], new Dictionary<string, BoardSpot>());

        var items = await store.ItemsAsync(map.ID);
        var pictures = items.Where(i => i.Type == CanvasItemType.Image).Select(PictureOf).OfType<MapPicture>().ToList();
        var places = items.Where(i => i.Type == CanvasItemType.Token)
            .Select(PlaceOf)
            .OfType<MapPlace>()
            .DistinctBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(p => p.Name, p => p.Spot, StringComparer.OrdinalIgnoreCase);

        return new CampaignMapView(CanvasIds.Encode(map.ID), FrameAround(pictures, places.Values), pictures, places);
    }

    /// <summary>
    /// Pins a place on the map, or moves it if it's there already, making the map first if there isn't one.
    /// </summary>
    public async Task PlaceAsync(string name, BoardSpot spot, string authorID)
    {
        var map = await FindAsync() ?? await store.CreateAsync(NewMapName, CanvasKind.Map, authorID);
        var existing = (await store.ItemsAsync(map.ID))
            .Where(i => i.Type == CanvasItemType.Token)
            .FirstOrDefault(i => PlaceOf(i) is { } place && string.Equals(place.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            await store.MoveItemAsync(map.ID, existing.ID, spot.X, spot.Y);
            await hub.Clients.Group(CanvasHub.Group(map.ID)).ItemMoved(authorID, existing.ID, spot.X, spot.Y);
            return;
        }

        var id = Guid.NewGuid().ToString();
        var token = JsonSerializer.SerializeToElement(new
        {
            attrs = new { x = spot.X, y = spot.Y, name },
            className = "Group",
            children = new[]
            {
                new { attrs = new { radius = PlaceRadius, fill = PlaceColor, stroke = "#ffffff", strokeWidth = 2 }, className = "Circle" },
            },
        });
        await store.SaveItemAsync(map.ID, id, CanvasItemType.Token, token, authorID);
        await hub.Clients.Group(CanvasHub.Group(map.ID)).ItemAdded(authorID, id, CanvasItemType.Token, token);
    }

    /// <summary>
    /// The stops the DM added by hand to each session, by the session's ID.
    /// </summary>
    public async Task<Dictionary<int, List<LoggedStop>>> LogsAsync()
    {
        if (await FindAsync() is not { } map)
            return [];

        return (await store.ItemsAsync(map.ID))
            .Where(i => i.Type == CanvasItemType.Stroke && i.ID.StartsWith(LogPrefix, StringComparison.Ordinal))
            .Select(LogOf)
            .OfType<SessionLog>()
            .ToDictionary(l => l.SessionID, l => l.Stops);
    }

    /// <summary>
    /// Replaces a session's hand-added stops, making the map first if there isn't one.
    /// </summary>
    public async Task SaveLogAsync(int sessionID, IReadOnlyList<LoggedStop> stops, string authorID)
    {
        var map = await FindAsync() ?? await store.CreateAsync(NewMapName, CanvasKind.Map, authorID);
        var id = $"{LogPrefix}{sessionID}";
        var item = JsonSerializer.SerializeToElement(new
        {
            attrs = new { id, name = "Journey log", visible = false, listening = false, journeyLog = sessionID, stops },
            className = "Group",
        }, Json);
        await store.SaveItemAsync(map.ID, id, CanvasItemType.Stroke, item, authorID);
    }

    private const string LogPrefix = "journey-log-";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static SessionLog? LogOf(CanvasItem item)
    {
        using var document = JsonDocument.Parse(item.Json);
        if (!document.RootElement.TryGetProperty("attrs", out var attrs)
            || !attrs.TryGetProperty("journeyLog", out var session)
            || !session.TryGetInt32(out var sessionID)
            || !attrs.TryGetProperty("stops", out var stops))
            return null;

        return new SessionLog(sessionID, stops.Deserialize<List<LoggedStop>>(Json) ?? []);
    }

    private async Task<Canvas?> FindAsync() =>
        (await store.ListAsync()).FirstOrDefault(c => c.Kind == CanvasKind.Map);

    // Around the pictures when there are some, since they're the map, and any places off them; else around the places
    // and where boards start, which is where the first ones get clicked
    private static MapFrame FrameAround(IReadOnlyList<MapPicture> pictures, IEnumerable<BoardSpot> places)
    {
        var boxes = pictures.Select(p => new MapFrame(p.X, p.Y, p.Width, p.Height))
            .Concat(places.Select(p => new MapFrame(p.X - Margin, p.Y - Margin, Margin * 2, Margin * 2)))
            .ToList();
        if (pictures.Count == 0)
            boxes.Add(Home);

        var left = boxes.Min(b => b.X);
        var top = boxes.Min(b => b.Y);
        var right = boxes.Max(b => b.X + b.Width);
        var bottom = boxes.Max(b => b.Y + b.Height);
        return new MapFrame(left, top, right - left, bottom - top);
    }

    // Konva saves a picture as { attrs: { x, y, width, height, src, scaleX, scaleY } }, leaving out zeros and ones
    private static MapPicture? PictureOf(CanvasItem item)
    {
        using var document = JsonDocument.Parse(item.Json);
        if (!document.RootElement.TryGetProperty("attrs", out var attrs) || Text(attrs, "src") is not { } src)
            return null;

        var width = Number(attrs, "width") * Number(attrs, "scaleX", 1);
        var height = Number(attrs, "height") * Number(attrs, "scaleY", 1);
        return width > 0 && height > 0 ? new MapPicture(src, Number(attrs, "x"), Number(attrs, "y"), width, height) : null;
    }

    private static MapPlace? PlaceOf(CanvasItem item)
    {
        using var document = JsonDocument.Parse(item.Json);
        if (!document.RootElement.TryGetProperty("attrs", out var attrs) || Text(attrs, "name") is not { Length: > 0 } name)
            return null;

        return new MapPlace(name, new BoardSpot(Number(attrs, "x"), Number(attrs, "y")));
    }

    private static string? Text(JsonElement attrs, string name) =>
        attrs.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double Number(JsonElement attrs, string name, double fallback = 0) =>
        attrs.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : fallback;

    private record MapPlace(string Name, BoardSpot Spot);

    private record SessionLog(int SessionID, List<LoggedStop> Stops);
}

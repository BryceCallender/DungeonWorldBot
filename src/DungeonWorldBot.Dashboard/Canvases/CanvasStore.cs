using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// Reads and saves canvases and what's drawn on them.
/// </summary>
public sealed class CanvasStore(CanvasContext db)
{
    public const int MaxNameLength = 80;
    public const int MaxItemIDLength = 64;

    // A long freehand stroke is a few thousand numbers; this leaves plenty of room without letting one item be huge
    public const int MaxItemLength = 256 * 1024;

    public Task<List<Canvas>> ListAsync(bool archived = false) =>
        db.Canvases.AsNoTracking()
            .Where(c => archived ? c.ArchivedAt != null : c.ArchivedAt == null)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

    public Task<Canvas?> FindAsync(int id) =>
        db.Canvases.AsNoTracking().FirstOrDefaultAsync(c => c.ID == id);

    public async Task<Canvas> CreateAsync(string name, CanvasKind kind, string? createdBy)
    {
        var now = DateTime.UtcNow;
        var canvas = new Canvas { Name = name, Kind = kind, CreatedBy = createdBy, CreatedAt = now, UpdatedAt = now };
        db.Canvases.Add(canvas);
        await db.SaveChangesAsync();
        return canvas;
    }

    /// <returns>Whether there was such a canvas.</returns>
    public async Task<bool> SetArchivedAsync(int id, bool archived) =>
        await db.Canvases.Where(c => c.ID == id)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ArchivedAt, archived ? DateTime.UtcNow : null)) > 0;

    /// <returns>Everything on the canvas, oldest first, so later strokes draw over earlier ones.</returns>
    public Task<List<CanvasItem>> ItemsAsync(int canvasID) =>
        db.CanvasItems.AsNoTracking()
            .Where(i => i.CanvasID == canvasID)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();

    /// <summary>
    /// Adds an item, or replaces it if the page sends the same ID again (like after reconnecting).
    /// </summary>
    public async Task SaveItemAsync(int canvasID, string itemID, CanvasItemType type, JsonElement item, string authorID)
    {
        var json = item.GetRawText();
        var existing = await db.CanvasItems.FindAsync(canvasID, itemID);
        if (existing is null)
            db.CanvasItems.Add(new CanvasItem
            {
                ID = itemID, CanvasID = canvasID, Type = type, Json = json, AuthorID = authorID, CreatedAt = DateTime.UtcNow,
            });
        else
        {
            existing.Type = type;
            existing.Json = json;
        }

        await db.SaveChangesAsync();
        await TouchAsync(canvasID);
    }

    /// <summary>
    /// Saves where an item was dragged to: its attrs.x and attrs.y for a Konva node, or x and y otherwise.
    /// </summary>
    /// <returns>Whether there was such an item.</returns>
    public async Task<bool> MoveItemAsync(int canvasID, string itemID, double x, double y)
    {
        var item = await db.CanvasItems.FindAsync(canvasID, itemID);
        if (item is null || JsonNode.Parse(item.Json) is not JsonObject root)
            return false;

        var position = root["attrs"] as JsonObject ?? root;
        position["x"] = x;
        position["y"] = y;
        item.Json = root.ToJsonString();

        await db.SaveChangesAsync();
        await TouchAsync(canvasID);
        return true;
    }

    public async Task RemoveItemAsync(int canvasID, string itemID)
    {
        if (await db.CanvasItems.Where(i => i.CanvasID == canvasID && i.ID == itemID).ExecuteDeleteAsync() > 0)
            await TouchAsync(canvasID);
    }

    /// <returns>The new image's ID.</returns>
    public async Task<string> SaveImageAsync(int canvasID, string contentType, byte[] data, string? uploadedBy)
    {
        // Random rather than counting up, so one image's link doesn't lead to the others
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        db.CanvasImages.Add(new CanvasImage
        {
            ID = id, CanvasID = canvasID, ContentType = contentType, Data = data, UploadedBy = uploadedBy, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    public Task<CanvasImage?> FindImageAsync(int canvasID, string imageID) =>
        db.CanvasImages.AsNoTracking().FirstOrDefaultAsync(i => i.CanvasID == canvasID && i.ID == imageID);

    /// <summary>
    /// Saves a canvas's picture, replacing the one before.
    /// </summary>
    public async Task SaveThumbnailAsync(int canvasID, string contentType, byte[] data)
    {
        var thumbnail = await db.CanvasThumbnails.FindAsync(canvasID);
        if (thumbnail is null)
            db.CanvasThumbnails.Add(thumbnail = new CanvasThumbnail { CanvasID = canvasID, ContentType = contentType, Data = data });
        else
        {
            thumbnail.ContentType = contentType;
            thumbnail.Data = data;
        }
        thumbnail.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public Task<bool> HasThumbnailAsync(int canvasID) => db.CanvasThumbnails.AnyAsync(t => t.CanvasID == canvasID);

    public Task<CanvasThumbnail?> FindThumbnailAsync(int canvasID) =>
        db.CanvasThumbnails.AsNoTracking().FirstOrDefaultAsync(t => t.CanvasID == canvasID);

    /// <returns>When each canvas that has a picture last had it taken, without loading the pictures.</returns>
    public Task<Dictionary<int, DateTime>> ThumbnailTimesAsync() =>
        db.CanvasThumbnails.AsNoTracking().ToDictionaryAsync(t => t.CanvasID, t => t.UpdatedAt);

    /// <returns>How many canvases are put away, for the "Archived" link.</returns>
    public Task<int> CountArchivedAsync() => db.Canvases.CountAsync(c => c.ArchivedAt != null);

    private Task TouchAsync(int canvasID) =>
        db.Canvases.Where(c => c.ID == canvasID)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
}

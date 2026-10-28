using System.Text.Json;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Scenes;

/// <summary>
/// Something already on the canvas, sent to a page as it opens.
/// </summary>
public record SavedItem(string ID, CanvasItemType Type, string AuthorID, JsonElement Item)
{
    public static SavedItem From(CanvasItem item)
    {
        using var json = JsonDocument.Parse(item.Json);
        return new SavedItem(item.ID, item.Type, item.AuthorID, json.RootElement.Clone());
    }
}

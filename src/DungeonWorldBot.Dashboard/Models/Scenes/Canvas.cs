namespace DungeonWorldBot.Dashboard.Models.Scenes;

/// <summary>
/// A scene: a board the table draws on together, for an encounter or a map.
/// </summary>
public class Canvas
{
    public int ID { get; set; }
    public required string Name { get; set; }
    public CanvasKind Kind { get; set; }

    /// <summary>
    /// The Discord ID of whoever made it, or null when sign-in is off.
    /// </summary>
    public string? CreatedBy { get; set; }

    // UTC DateTimes rather than DateTimeOffsets, which SQLite can't sort by
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// When it was put away. Archived canvases are hidden from the list but kept.
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    public List<CanvasItem> Items { get; set; } = [];
}

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// One thing on a canvas: a stroke, a token or some text.
/// </summary>
public class CanvasItem
{
    public const int MaxIDLength = 64;

    /// <summary>
    /// Made by the page that drew it, so it can be drawn, moved or removed before the server has seen it.
    /// </summary>
    public required string ID { get; set; }

    public int CanvasID { get; set; }
    public Canvas? Canvas { get; set; }

    public CanvasItemType Type { get; set; }

    /// <summary>
    /// The item as the page draws it, like a Konva node's toObject().
    /// </summary>
    public required string Json { get; set; }

    /// <summary>
    /// Their Discord ID, or a guest ID when sign-in is off.
    /// </summary>
    public required string AuthorID { get; set; }

    public DateTime CreatedAt { get; set; }
}

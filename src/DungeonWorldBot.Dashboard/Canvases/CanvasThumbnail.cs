namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// A small picture of a whole canvas, for its card on the canvases page. The board takes it after changes.
/// </summary>
/// <remarks>
/// Its own table rather than a column on <see cref="Canvas"/>, so listing canvases doesn't load every picture.
/// </remarks>
public class CanvasThumbnail
{
    public int CanvasID { get; set; }
    public Canvas? Canvas { get; set; }

    /// <summary>
    /// Worked out from the file's first bytes, never taken from what the browser says it is.
    /// </summary>
    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    /// <summary>
    /// When it was taken, which also goes in its URL so browsers fetch a new one when it changes.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

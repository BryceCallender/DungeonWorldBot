namespace DungeonWorldBot.Dashboard.Canvases;

/// <summary>
/// A picture someone added to a canvas, for an image token or a map. Items point at it by URL.
/// </summary>
/// <remarks>
/// Kept in the database, so it's backed up with the canvas and goes when the canvas does. The page shrinks pictures
/// before uploading them, so they're small.
/// </remarks>
public class CanvasImage
{
    public required string ID { get; set; }

    public int CanvasID { get; set; }
    public Canvas? Canvas { get; set; }

    /// <summary>
    /// Worked out from the file's first bytes, never taken from what the browser says it is.
    /// </summary>
    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    /// <summary>
    /// Their Discord ID, or null when sign-in is off.
    /// </summary>
    public string? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}

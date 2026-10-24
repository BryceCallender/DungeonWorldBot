namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// A picture of a character, set on their page. Their tokens on the board show it.
/// </summary>
/// <remarks>
/// Kept in the dashboard's own database (<see cref="Canvases.CanvasContext"/>), since the bot's is only read. The page
/// shrinks pictures before uploading them, so they're small.
/// </remarks>
public class CharacterPortrait
{
    /// <summary>
    /// The character's ID, which is their player's Discord ID.
    /// </summary>
    public ulong CharacterID { get; set; }

    /// <summary>
    /// Worked out from the file's first bytes, never taken from what the browser says it is.
    /// </summary>
    public required string ContentType { get; set; }

    public required byte[] Data { get; set; }

    /// <summary>
    /// When it was set, which also goes in its URL so browsers fetch a new one when it changes.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

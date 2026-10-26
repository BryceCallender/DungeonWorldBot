namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A game night, from when someone at the table starts it with /session start to when it ends with /session end. One goes
/// on at a time. Sessions are numbered by the order they started in.
/// </summary>
public class GameSession
{
    public int ID { get; set; }

    /// <summary>
    /// What the night's about, if the DM named it, e.g. "Into the Sunken Abbey".
    /// </summary>
    public string? Title { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Null while it's going on.
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }
}

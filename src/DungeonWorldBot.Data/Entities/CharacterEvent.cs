using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// Something that changed on a character, like a level gained or a move learned, kept for their timeline. Recorded by
/// <see cref="CharacterEvents"/> whenever the change is saved, whoever made it, so there's nothing before it shipped.
/// </summary>
public class CharacterEvent
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }

    public DateTimeOffset At { get; set; }

    public CharacterEventKind Kind { get; set; }

    /// <summary>
    /// What it was about, if anything: the move, item, bond or goal's name, the stat, or the new class.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// The number before, for changes like a level or a stat.
    /// </summary>
    public int? From { get; set; }

    /// <summary>
    /// The number after, for changes like a level or a stat, or how many of an item.
    /// </summary>
    public int? To { get; set; }
}

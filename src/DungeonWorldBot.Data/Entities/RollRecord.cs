using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A 2d6 roll someone made, kept for roll stats. Keyed by the bot's roll message so the same roll is never counted
/// twice, whether it was recorded live or found by scanning chat history. When it was rolled comes from the message ID.
/// </summary>
public class RollRecord
{
    public Snowflake MessageID { get; set; }
    public Snowflake ChannelID { get; set; }
    public Snowflake UserID { get; set; }

    // The two d6 as rolled, before modifiers
    public int First { get; set; }
    public int Second { get; set; }

    // With modifiers, which decides the 10+ / 7-9 / 6- band
    public int Total { get; set; }

    /// <summary>
    /// Everything added to the dice, so <see cref="Total"/> is the dice plus this.
    /// </summary>
    public int Modifier { get; set; }

    /// <summary>
    /// Where the modifier came from, as the roll's "Rolled with" showed it, e.g. "STR +2, Weak -1, Bonus +1". Null when
    /// the roll didn't say.
    /// </summary>
    public string? Modifiers { get; set; }

    /// <summary>
    /// The move rolled, like "Hack and Slash", if the roll was for one.
    /// </summary>
    public string? Move { get; set; }

    /// <summary>
    /// The stat rolled with, if any. Moves that roll a class resource or bonds have none.
    /// </summary>
    public StatType? Stat { get; set; }

    /// <summary>
    /// What the roll counts as instead of its total's band, when someone changed it after it was posted for a rule the
    /// bot doesn't track. Null if it wasn't changed.
    /// </summary>
    public MoveOutcome? ChangedOutcome { get; set; }

    /// <summary>
    /// Why the outcome was changed, e.g. "Blessing of Luna".
    /// </summary>
    public string? ChangedReason { get; set; }

    /// <summary>
    /// Who changed the outcome.
    /// </summary>
    public Snowflake? ChangedBy { get; set; }
}

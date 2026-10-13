using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A 2d6 roll someone made, kept for roll stats. Keyed by the bot's roll message so the same roll is never counted
/// twice, whether it was recorded live or found by scanning chat history.
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
}

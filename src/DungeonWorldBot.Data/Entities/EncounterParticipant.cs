using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A player's character or an NPC the DM picked to be in a fight. One or the other is set.
/// </summary>
public class EncounterParticipant
{
    public int ID { get; set; }

    public int EncounterID { get; set; }

    public Encounter Encounter { get; set; } = null!;

    /// <summary>
    /// The player's character, if it's one.
    /// </summary>
    public Snowflake? CharacterID { get; set; }

    /// <summary>
    /// The NPC, if it's one. They drop out of the fight if they leave the party.
    /// </summary>
    public int? NpcID { get; set; }
}

using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// Something someone did in a fight, for the spotlight: a player's move, an NPC's roll or an enemy's attack, with what it
/// rolled and who it was aimed at.
/// </summary>
public class EncounterAction
{
    public int ID { get; set; }

    public int EncounterID { get; set; }

    public Encounter Encounter { get; set; } = null!;

    public DateTimeOffset At { get; set; }

    /// <summary>
    /// Who acted, as they were called at the time. Kept even if the enemy leaves the fight.
    /// </summary>
    public string Actor { get; set; } = null!;

    /// <summary>
    /// The player's character who acted, if it was one.
    /// </summary>
    public Snowflake? CharacterID { get; set; }

    /// <summary>
    /// The enemy who acted, if it was one. Cleared if they're taken out of the fight.
    /// </summary>
    public int? EnemyID { get; set; }

    /// <summary>
    /// The NPC who acted, if it was one. Cleared if they leave the party.
    /// </summary>
    public int? NpcID { get; set; }

    /// <summary>
    /// The bot's roll message, when the action was a 2d6 roll that's also kept for roll stats.
    /// </summary>
    public Snowflake? MessageID { get; set; }

    /// <summary>
    /// e.g. "Hack and Slash", or "Attack".
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// What it rolled, e.g. "9 · Weak Hit (7-9)" or "d8+1 = 6".
    /// </summary>
    public string? Roll { get; set; }

    /// <summary>
    /// Who it was aimed at, if anyone.
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// What happened to the target, e.g. "took 4".
    /// </summary>
    public string? Result { get; set; }
}

using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public enum MoveRequestKind
{
    HackAndSlash,
    Volley,

    /// <summary>
    /// An enemy attacking a player's character or an NPC.
    /// </summary>
    EnemyAttack,
}

/// <summary>
/// A move someone made from a fight's scene on the dashboard. The dashboard can't roll it or reach Discord, so it only
/// asks, and the bot rolls it by the same rules as its commands and tells game chat a moment later.
/// </summary>
/// <remarks>
/// Who acts is <see cref="CharacterID"/> or <see cref="EnemyID"/>, and who it's aimed at is one of the targets.
/// </remarks>
public class MoveRequest
{
    public int ID { get; set; }

    public int EncounterID { get; set; }

    public Encounter Encounter { get; set; } = null!;

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>
    /// Who asked, by Discord ID. Null when the dashboard's sign-in is off.
    /// </summary>
    public Snowflake? RequestedBy { get; set; }

    public MoveRequestKind Kind { get; set; }

    /// <summary>
    /// The player's character making the move, if it's one.
    /// </summary>
    public Snowflake? CharacterID { get; set; }

    /// <summary>
    /// The enemy attacking, if it's one. Cleared if they're taken out of the fight.
    /// </summary>
    public int? EnemyID { get; set; }

    public int? TargetEnemyID { get; set; }

    public Snowflake? TargetCharacterID { get; set; }

    public int? TargetNpcID { get; set; }

    /// <summary>
    /// Any +forward, ongoing bonus or penalty added to a player's roll.
    /// </summary>
    public int Bonus { get; set; }

    /// <summary>
    /// Flat damage added to a player's damage roll.
    /// </summary>
    public int ExtraDamage { get; set; }

    /// <summary>
    /// What an enemy does, e.g. Bite. A plain attack if not given.
    /// </summary>
    public string? Move { get; set; }

    /// <summary>
    /// When the bot rolled it, or gave up on it. Null while it's waiting.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Why the bot couldn't make the move, if it couldn't.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// A few words for the board to show over the target, e.g. "Miss" or "-4".
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// What the move did, in the fight's spotlight.
    /// </summary>
    public int? ActionID { get; set; }
}

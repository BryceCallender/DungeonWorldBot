using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// Someone the GM plays who travels with the party: a hireling, a guide, an animal companion, a rescued prisoner.
/// </summary>
public class Npc
{
    public int ID { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// What they are to the party, e.g. "Hireling", "Guide", "Animal companion".
    /// </summary>
    public string? Role { get; set; }

    public int CurrentHP { get; set; }

    public int MaxHP { get; set; }

    /// <summary>
    /// Reduces physical damage, like a character's armor.
    /// </summary>
    public int Armor { get; set; }

    /// <summary>
    /// Dice they roll for damage, e.g. "d6".
    /// </summary>
    public string? Damage { get; set; }

    /// <summary>
    /// A hireling's loyalty, added when they're asked to do something dangerous or degrading.
    /// </summary>
    public int Loyalty { get; set; }

    /// <summary>
    /// A hireling's skills, e.g. "Protector +2, Tracker +1".
    /// </summary>
    public string? Skills { get; set; }

    /// <summary>
    /// What a hireling wants in return for their service, e.g. "Money" or "Glory".
    /// </summary>
    public string? Cost { get; set; }

    public string? Notes { get; set; }

    // Stat modifiers (-3 to +3) rather than scores: NPCs are usually written as "STR +2"
    public int Str { get; set; }
    public int Dex { get; set; }
    public int Con { get; set; }
    public int Int { get; set; }
    public int Wis { get; set; }
    public int Cha { get; set; }

    public int Modifier(StatType type) => type switch
    {
        StatType.Str => Str,
        StatType.Dex => Dex,
        StatType.Con => Con,
        StatType.Int => Int,
        StatType.Wis => Wis,
        StatType.Cha => Cha,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public void SetModifier(StatType type, int value)
    {
        switch (type)
        {
            case StatType.Str: Str = value; break;
            case StatType.Dex: Dex = value; break;
            case StatType.Con: Con = value; break;
            case StatType.Int: Int = value; break;
            case StatType.Wis: Wis = value; break;
            case StatType.Cha: Cha = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    /// <summary>
    /// The player whose character they follow, if any (a Ranger's animal companion, a Necromancer's thrall).
    /// </summary>
    public Snowflake? FollowsID { get; set; }
}

using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Character
{
    public Snowflake ID { get; set; }

    public string Name { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public Health Health { get; set; } = null!;
    public Inventory Inventory { get; set; } = null!;
    
    /// <summary>
    /// Physical armor from the character's gear and moves.
    /// </summary>
    public int ArmorRating { get; set; }

    public int MagicArmor { get; set; }

    /// <summary>
    /// Armor that only divine weapons and powers can get through.
    /// </summary>
    public int DivineArmor { get; set; }
    
    public int DamageRating { get; set; }
    
    public int Level { get; set; } = 1;

    public List<Stat> Stats { get; set; } = null!;
    public List<Bond>? Bonds { get; set; }
    public List<Debility>? Debilities { get; set; }
    public List<Ability>? Abilities { get; set; }
    public List<CharacterResource>? Resources { get; set; }
    public List<ArmorBonus>? ArmorBonuses { get; set; }
    public List<Transformation>? Transformations { get; set; }
    public List<Goal>? Goals { get; set; }
    public List<Note>? Notes { get; set; }

    public Status? Status { get; set; }
    public Alignment Alignment { get; set; }
    public Race Race { get; set; }
    public Location? Location { get; set; }

    public string? DamageModifier { get; set; }

    /// <summary>
    /// Where the character comes from, written by their player on the dashboard.
    /// </summary>
    public string? Backstory { get; set; }

    /// <summary>
    /// Their picture, set on the dashboard. Only there when included.
    /// </summary>
    public CharacterPortrait? Portrait { get; set; }
}
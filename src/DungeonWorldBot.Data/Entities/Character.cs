using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Character
{
    /// <summary>
    /// The player's Discord ID, or a made-up one for a character run by someone else (see <see cref="ControllerID"/>).
    /// </summary>
    public Snowflake ID { get; set; }

    /// <summary>
    /// The player who runs this character alongside their own, like an NPC the DM handed to them.
    /// </summary>
    public Snowflake? ControllerID { get; set; }

    /// <summary>
    /// Whether their controller is playing them right now, so the controller's commands act on them instead of on
    /// their own character.
    /// </summary>
    public bool InPlay { get; set; }

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
}
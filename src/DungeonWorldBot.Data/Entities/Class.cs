using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Class
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; } = null!;

    public ClassType Type { get; set; }

    public string Damage
    {
        get
        {
            return Type switch
            {
                ClassType.Ranger => "d8",
                ClassType.Channeler => "d4",
                ClassType.Necromancer => "d4",
                ClassType.Paladin => "d10",
                ClassType.Slayer => "d10",
                ClassType.Druid => "d6",
                ClassType.Battlemaster => "d8",
                ClassType.Templar => "d8",
                ClassType.Shaman => "d6",
                ClassType.Captain => "d6",
                ClassType.Collector => "d4",
                ClassType.Lantern => "d6",
                ClassType.Mechanic => "d8",
                ClassType.SkyDancer => "d6",
                ClassType.Survivor => "d10",
                ClassType.Walker => "d8",
                ClassType.Golem => "d8",
                ClassType.Barbarian => "d10",
                ClassType.Bard => "d6",
                ClassType.Cleric => "d6",
                ClassType.Fighter => "d10",
                ClassType.Immolator => "d4",
                ClassType.Thief => "d8",
                ClassType.Wizard => "d4",
                _ => "d6"
            };
        }
    }

    public int HealthModifier
    {
        get
        {
            return Type switch
            {
                ClassType.Ranger => 8,
                ClassType.Channeler => 10,
                ClassType.Necromancer => 4,
                ClassType.Paladin => 10,
                ClassType.Slayer => 8,
                ClassType.Druid => 6,
                ClassType.Battlemaster => 10,
                ClassType.Templar => 8,
                ClassType.Shaman => 6,
                ClassType.Captain => 8,
                ClassType.Collector => 6,
                ClassType.Lantern => 4,
                ClassType.Mechanic => 6,
                ClassType.SkyDancer => 6,
                ClassType.Survivor => 10,
                ClassType.Walker => 8,
                ClassType.Golem => 10,
                ClassType.Barbarian => 8,
                ClassType.Bard => 6,
                ClassType.Cleric => 8,
                ClassType.Fighter => 10,
                ClassType.Immolator => 4,
                ClassType.Thief => 6,
                ClassType.Wizard => 4,
                _ => 4
            };
        }
    }
}
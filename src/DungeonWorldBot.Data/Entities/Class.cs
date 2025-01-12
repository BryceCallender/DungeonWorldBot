using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Class
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; }

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
                _ => 4
            };
        }
    }
}
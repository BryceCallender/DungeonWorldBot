using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Class
{
    public Snowflake ID { get; set; }

    public ClassType ClassType { get; set; }

    public string Damage
    {
        get
        {
            return ClassType switch
            {
                ClassType.Ranger => "d8",
                ClassType.Channeler => "d4",
                ClassType.Necromancer => "d4",
                ClassType.Paladin => "d10",
                ClassType.Slayer => "d10",
                _ => "d6"
            };
        }
    }

    public int HealthModifier
    {
        get
        {
            return ClassType switch
            {
                ClassType.Ranger => 8,
                ClassType.Channeler => 10,
                ClassType.Necromancer => 4,
                ClassType.Paladin => 10,
                ClassType.Slayer => 8,
                _ => 4
            };
        }
    }
}
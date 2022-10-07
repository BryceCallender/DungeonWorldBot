using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Class
{
    public Snowflake ID { get; set; }

    public ClassType ClassType { get; set; }

    public string Damage { get; set; }
    public int HealthModifier { get; set; }



}
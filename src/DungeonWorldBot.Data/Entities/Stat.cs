using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Stat
{
    public Snowflake ID { get; set; }
    
    public StatType StatType { get; set; }
    public int Modifier { get; set; }  
}
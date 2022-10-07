using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Inventory
{
    public Snowflake ID { get; set; }

    public int CurrentLoad { get; set; }
    public int MaxLoad { get; set; }
    
    public List<Item>? Items { get; set; }
}
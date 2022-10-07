using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Item
{
    public Snowflake ID { get; set; }

    public string Name { get; set; }
    public int Amount { get; set; } = 1;
    public int Weight { get; set; }
}
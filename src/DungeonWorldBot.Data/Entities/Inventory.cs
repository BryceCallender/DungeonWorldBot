using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Inventory
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; } = null!;

    public int CurrentLoad { get; set; }
    public int MaxLoad { get; set; }
    
    public List<Item>? Items { get; set; }
}
using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Character
{
    public Snowflake ID { get; set; }

    public string Name { get; set; } = null!;
    public Class Class { get; set; } = null!;
    public Health Health { get; set; } = null!;
    public Inventory? Inventory { get; set; }
    
    public int ArmorRating { get; set; } = 0;
    public int Level { get; set; } = 1;

    public List<Stat> Stats { get; set; } = null!;
    public List<Bond>? Bonds { get; set; }
    public List<Debility>? Debilities { get; set; }

    public Status? Status { get; set; }
    public Alignment Alignment { get; set; }
    public Race Race { get; set; }
    public Location? Location { get; set; }
}
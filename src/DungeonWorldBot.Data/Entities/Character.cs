using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Character
{
    public Snowflake ID { get; set; }

    public string Name { get; set; }
    public string Class { get; set; }
    public int Level { get; set; }
    
    public List<Stat> Stats { get; set; }
    public List<Bond> Bonds { get; set; }
    public List<Debility> Debilities { get; set; }

    public Status Status { get; set; }
    public Alignment Alignment { get; set; }
    public Race Race { get; set; }
    public Location Location { get; set; }
}
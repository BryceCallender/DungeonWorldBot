using Remora.Rest.Core;

namespace DDBot.API.Models;

public class Character
{
    public Snowflake ID { get; set; }
    
    public string Name { get; set; }
    public string Class { get; set; }
    public int Level { get; set; }
    
    public Dictionary<StatType, Stat> Stats { get; set; }
    public List<Bond> Bonds { get; set; }
    public Alignment Alignment { get; set; }
    public Race Race { get; set; }
    public List<Debility> Debilities { get; set; }
}
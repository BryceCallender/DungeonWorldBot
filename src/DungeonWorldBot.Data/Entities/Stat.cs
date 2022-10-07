using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Stat
{
    public int ID { get; set; }
    
    public StatType StatType { get; set; }

    public int Value { get; set; }

    public int Modifier
    {
        get
        {
            return Value switch
            {
                (<= 3) => -3,
                (<= 5) => -2,
                (<= 8) => -1,
                (<= 12) => 0,
                (<= 15) => 1,
                (<= 17) => 2,
                _ => 3
            };
        }
    }
}
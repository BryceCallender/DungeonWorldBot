using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Health
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; }
    
    public int CurrentHP { get; set; }
    
    public int MaxHP { get; set; }

    public string ToDisplay()
    {
        return $"{CurrentHP} / {MaxHP} ({(CurrentHP / (double)MaxHP * 100).ToString("0.##")}%)";
    }
}
using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Debility
{
    public int ID { get; set; }

    public StatType StatType { get; set; }
    public int Modifier { get; set; }
}
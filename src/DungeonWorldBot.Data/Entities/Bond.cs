using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

public class Bond
{
    public int ID { get; set; }
    public Snowflake TargetID { get; set; }
    public string TargetName { get; set; }
}
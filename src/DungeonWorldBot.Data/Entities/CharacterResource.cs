using Remora.Rest.Core;

namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// The current value of a class resource, like a Battlemaster's Gambits or a Slayer's Thirst.
/// </summary>
public class CharacterResource
{
    public int ID { get; set; }

    public Snowflake CharacterID { get; set; }
    public Character Character { get; set; } = null!;

    public string Name { get; set; } = null!;
    public int Value { get; set; }
}

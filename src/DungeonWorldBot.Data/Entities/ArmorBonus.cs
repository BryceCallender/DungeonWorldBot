namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// Temporary armor on top of a character's own, like +1 magical armor from a Lightning barrier.
/// </summary>
public class ArmorBonus
{
    public int ID { get; set; }

    public string Source { get; set; } = null!;
    public ArmorType Type { get; set; }
    public int Amount { get; set; }
    public ArmorDuration Duration { get; set; }
}

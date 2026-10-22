namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A monster the GM has written up once, to spawn into fights as many times as needed.
/// </summary>
public class MonsterTemplate
{
    public int ID { get; set; }

    public string Name { get; set; } = null!;

    public int MaxHP { get; set; }

    public int Armor { get; set; }

    public string? Damage { get; set; }

    public string? Tags { get; set; }

    public string? Instinct { get; set; }

    /// <summary>
    /// The GM moves it makes, separated by semicolons or new lines.
    /// </summary>
    public string? Moves { get; set; }

    public string? Notes { get; set; }
}

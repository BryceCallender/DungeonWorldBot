namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// One of a player's notes about their character, written on the dashboard.
/// </summary>
public class Note
{
    public int ID { get; set; }

    public string Text { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}

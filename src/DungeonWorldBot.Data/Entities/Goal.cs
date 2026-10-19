namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// Something a player wants their character to accomplish, written on the dashboard.
/// </summary>
public class Goal
{
    public int ID { get; set; }

    public string Text { get; set; } = null!;

    public bool Completed { get; set; }
}

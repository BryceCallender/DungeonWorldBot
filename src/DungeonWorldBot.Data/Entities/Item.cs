namespace DungeonWorldBot.Data.Entities;

public class Item
{
    public int ID { get; set; }

    public string Name { get; set; }
    public int Amount { get; set; } = 1;
    public int Weight { get; set; }
}
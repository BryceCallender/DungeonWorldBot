namespace DungeonWorldBot.Data.Entities;

public class Armor : Item
{
    public ArmorType Type { get; set; }
    
    public int ArmorRating { get; set; }
}
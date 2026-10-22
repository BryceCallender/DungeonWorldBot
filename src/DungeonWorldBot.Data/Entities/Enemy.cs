namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// A monster the party is fighting, in the current encounter.
/// </summary>
public class Enemy
{
    public int ID { get; set; }

    public string Name { get; set; } = null!;

    public int EncounterID { get; set; }

    public Encounter Encounter { get; set; } = null!;

    public int CurrentHP { get; set; }

    public int MaxHP { get; set; }

    /// <summary>
    /// Reduces physical damage, like a character's armor.
    /// </summary>
    public int Armor { get; set; }

    /// <summary>
    /// Dice they roll for damage, e.g. "d8+1".
    /// </summary>
    public string? Damage { get; set; }

    /// <summary>
    /// e.g. "Horde, Small, Intelligent".
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// What it wants to do, e.g. "To overwhelm".
    /// </summary>
    public string? Instinct { get; set; }

    /// <summary>
    /// The GM moves it makes, separated by semicolons or new lines.
    /// </summary>
    public string? Moves { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// How hurt it looks, which is all the players see.
    /// </summary>
    public EnemyCondition Condition => EnemyConditions.Of(CurrentHP, MaxHP);
}

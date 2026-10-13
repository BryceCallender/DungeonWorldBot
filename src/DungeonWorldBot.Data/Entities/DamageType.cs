namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// What kind of damage a hit deals, which decides the armor that reduces it.
/// </summary>
public enum DamageType
{
    Physical,
    Magical,

    /// <summary>
    /// Ignores all armor.
    /// </summary>
    True
}

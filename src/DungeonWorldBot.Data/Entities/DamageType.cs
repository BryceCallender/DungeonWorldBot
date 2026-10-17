namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// What kind of damage a hit deals, which decides the armor that reduces it.
/// </summary>
public enum DamageType
{
    Physical,
    Magical,

    /// <summary>
    /// From divine weapons and powers. Only divine armor reduces it.
    /// </summary>
    Divine,

    /// <summary>
    /// Ignores all armor.
    /// </summary>
    True
}

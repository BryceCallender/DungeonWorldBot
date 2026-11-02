namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// What a Channeler's Technique does. A Technique has several, and each one only once.
/// </summary>
[Flags]
public enum TechniqueTag
{
    None = 0,

    /// <summary>
    /// 1d6 damage if near.
    /// </summary>
    Projectile = 1,

    /// <summary>
    /// +1 armor until the next Channel.
    /// </summary>
    Barrier = 2,

    /// <summary>
    /// 1d8 damage if close.
    /// </summary>
    Blast = 4,

    /// <summary>
    /// An environmental effect.
    /// </summary>
    Hazard = 8,

    /// <summary>
    /// 2 piercing.
    /// </summary>
    Piercing = 16,

    Forceful = 32,

    /// <summary>
    /// Instantly move somewhere near. Needs the Teleport move.
    /// </summary>
    Teleport = 64,

    /// <summary>
    /// Affects up to 3 targets near each other. Needs the Area Burst move.
    /// </summary>
    Burst = 128,
}

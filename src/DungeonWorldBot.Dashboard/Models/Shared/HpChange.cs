namespace DungeonWorldBot.Dashboard.Models.Shared;

/// <summary>
/// Which way the DM's quick HP buttons move someone's health.
/// </summary>
public enum HpChange
{
    /// <summary>
    /// Reduced by their armor, as a physical hit is.
    /// </summary>
    Damage,

    Heal,
}

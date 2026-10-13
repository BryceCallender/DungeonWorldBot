namespace DungeonWorldBot.Data.Entities;

/// <summary>
/// How long temporary armor lasts.
/// </summary>
public enum ArmorDuration
{
    /// <summary>
    /// Until the fight ends, like Guardian's Stand.
    /// </summary>
    Fight,

    /// <summary>
    /// Until the next hit lands, like a Lightning barrier. It still reduces that hit.
    /// </summary>
    UntilHit,

    /// <summary>
    /// Until it's removed by hand.
    /// </summary>
    UntilRemoved
}

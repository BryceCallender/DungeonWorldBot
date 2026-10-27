using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Helpers;

/// <summary>
/// The CSS classes rolls are colored with.
/// </summary>
public static class RollStyles
{
    // Fewer rolls than this is too few to call anyone's dice lucky or not
    private const int LuckRolls = 10;

    /// <summary>
    /// "strong", "weak" or "miss".
    /// </summary>
    public static string OutcomeClass(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => "strong",
        MoveOutcome.WeakHit => "weak",
        _ => "miss",
    };

    /// <summary>
    /// "lucky" or "unlucky" when the dice averaged half a pip from fair over enough rolls, else nothing.
    /// </summary>
    public static string Luck(int rolls, double averageNatural) => rolls < LuckRolls ? ""
        : averageNatural >= 7.5 ? "lucky"
        : averageNatural <= 6.5 ? "unlucky"
        : "";
}

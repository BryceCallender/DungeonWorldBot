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
    /// A fight roll's pill colour from its text: "strong", "weak" or "miss" for a player's ("9 · Weak Hit (7-9)"), and
    /// "plain" for an enemy's dice ("d12 = 7").
    /// </summary>
    public static string FromRollText(string roll, bool missed = false) =>
        missed || roll.Contains("Miss") ? "miss"
        : roll.Contains("Strong Hit") ? "strong"
        : roll.Contains("Weak Hit") ? "weak"
        : "plain";

    /// <summary>
    /// "lucky" or "unlucky" when the dice averaged half a pip from fair over enough rolls, else nothing.
    /// </summary>
    public static string Luck(int rolls, double averageNatural) => rolls < LuckRolls ? ""
        : averageNatural >= 7.5 ? "lucky"
        : averageNatural <= 6.5 ? "unlucky"
        : "";
}

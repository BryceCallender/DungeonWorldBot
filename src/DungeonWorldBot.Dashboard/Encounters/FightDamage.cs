using System.Text.RegularExpressions;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// Reading the damage back out of what a fight's timeline says a hit did.
/// </summary>
public static partial class FightDamage
{
    // "💥 4 damage", as the bot writes what a hit did
    [GeneratedRegex(@"💥\s*(\d+)\s*damage")]
    private static partial Regex DamageResult();

    /// <param name="result">An action's result, like "💥 4 damage".</param>
    /// <returns>The damage, or 0 if it didn't say any.</returns>
    public static int Of(string? result) =>
        result is not null && DamageResult().Match(result) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;

    // "Goblin 2" → "Goblin", as fights number several of the same monster
    [GeneratedRegex(@"\s+\d+$")]
    private static partial Regex Numbered();

    /// <summary>
    /// What kind of monster an enemy is, from its name without the number it was given in its fight.
    /// </summary>
    public static string Kind(string name) => Numbered().Replace(name.Trim(), "");
}

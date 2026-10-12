using System.Text;
using d20Tek.DiceNotation.Results;

namespace DungeonWorldBot.Helpers;

public static class DiceHelper
{
    /// <summary>
    /// Appends a signed modifier to a dice expression, e.g. ("2d6", -1) => "2d6-1".
    /// </summary>
    public static string AddModifier(string expression, int modifier) => modifier switch
    {
        > 0 => $"{expression}+{modifier}",
        < 0 => $"{expression}{modifier}",
        _ => expression
    };

    /// <summary>
    /// Formats a roll for an embed, e.g. "`4d6k3`\nRolls: 6, 5, 3, ~~2~~\nTotal: **14**".
    /// </summary>
    public static string BuildDiceResult(DiceResult roll)
    {
        var text = new StringBuilder();
        text.AppendLine($"`{roll.DiceExpression}`");

        if (roll.Results.Count > 0)
        {
            // Dice that were dropped (e.g. by k3) are struck through so it's clear why they don't count
            var rolls = roll.Results.Select(r => r.AppliesToResultCalculation ? $"{r.Value}" : $"~~{r.Value}~~");
            text.AppendLine($"Rolls: {string.Join(", ", rolls)}");
        }

        text.Append($"Total: **{roll.Value}**");
        return text.ToString();
    }
}

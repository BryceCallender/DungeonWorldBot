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
    /// Formats a roll the way you'd add it up at the table, e.g. for "2d6+1":
    /// <code>
    /// d6 + d6 + 1
    ///
    /// 4 + 1 + 1 = **6**
    /// </code>
    /// Dropped dice (e.g. from "4d6k3") are struck through.
    /// </summary>
    public static string BuildDiceResult(DiceResult roll)
    {
        var dice = roll.Results;
        if (dice.Count == 0)
            return $"**{roll.Value}**";

        // With x, / or brackets the total isn't a simple sum of the dice, so show the expression and the dice instead
        if (roll.DiceExpression.IndexOfAny(['x', 'X', '/', '(']) >= 0)
        {
            var values = string.Join(", ", dice.Select(d => Strike($"{d.Value}", d)));
            return $"{roll.DiceExpression}\n\n{values} → **{roll.Value}**";
        }

        // Whatever the kept dice don't account for is the flat modifier, e.g. the +1 in "2d6+1"
        var modifier = roll.Value - dice.Where(d => d.AppliesToResultCalculation).Sum(d => d.Value);

        if (dice.Count == 1 && modifier == 0)
            return $"{DieName(dice[0])}\n\n**{roll.Value}**";

        var names = new StringBuilder(Strike(DieName(dice[0]), dice[0]));
        var sums = new StringBuilder(Strike($"{dice[0].Value}", dice[0]));

        foreach (var die in dice.Skip(1))
        {
            names.Append($" + {Strike(DieName(die), die)}");
            sums.Append(die.Value < 0
                ? $" - {Strike($"{-die.Value}", die)}"
                : $" + {Strike($"{die.Value}", die)}");
        }

        if (modifier != 0)
        {
            var signed = modifier > 0 ? $" + {modifier}" : $" - {-modifier}";
            names.Append(signed);
            sums.Append(signed);
        }

        return $"{names}\n\n{sums} = **{roll.Value}**";
    }

    // "DiceTerm.d6" => "d6", "FudgeDiceTerm.dF" => "dF"
    private static string DieName(TermResult die) => die.Type[(die.Type.LastIndexOf('.') + 1)..];

    // Dice that were dropped (e.g. by k3) are struck through so it's clear why they don't count
    private static string Strike(string text, TermResult die) => die.AppliesToResultCalculation ? text : $"~~{text}~~";
}

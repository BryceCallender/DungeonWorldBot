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
    /// Formats a roll the way you'd add it up at the table, with each die's result lined up under it:
    /// <code>
    /// d6 + d6 + 1
    ///  4 +  2 + 1 = 7
    /// </code>
    /// It's a code block so the columns line up. Dropped dice (e.g. from "4d6k3") are left out of the sum and
    /// listed on a "dropped:" line underneath.
    /// </summary>
    public static string BuildDiceResult(DiceResult roll)
    {
        var dice = roll.Results;
        if (dice.Count == 0)
            return $"**{roll.Value}**";

        // With x, / or brackets the total isn't a simple sum of the dice, so show the expression and the dice instead
        if (roll.DiceExpression.IndexOfAny(['x', 'X', '/', '(']) >= 0)
        {
            var values = string.Join(", ", dice.Select(d => d.AppliesToResultCalculation ? $"{d.Value}" : $"~~{d.Value}~~"));
            return $"`{roll.DiceExpression}`\n{values} → **{roll.Value}**";
        }

        // Only dice that count go in the sum so the math adds up; dropped ones (e.g. from k3) are listed underneath
        var kept = dice.Where(d => d.AppliesToResultCalculation).ToList();
        var dropped = dice.Where(d => !d.AppliesToResultCalculation).ToList();

        var columns = new List<(string Operator, string Die, string Value)>
        {
            ("", DieName(kept[0]), $"{kept[0].Value}"),
        };

        foreach (var die in kept.Skip(1))
        {
            var value = $"{Math.Abs(die.Value)}";
            columns.Add((die.Value < 0 ? "-" : "+", DieName(die), value));
        }

        // Whatever the kept dice don't account for is the flat modifier, e.g. the +1 in "2d6+1"
        var modifier = roll.Value - kept.Sum(d => d.Value);
        if (modifier != 0)
        {
            var amount = $"{Math.Abs(modifier)}";
            columns.Add((modifier > 0 ? "+" : "-", amount, amount));
        }

        var dieLine = new StringBuilder();
        var valueLine = new StringBuilder();

        foreach (var (op, die, value) in columns)
        {
            if (op.Length > 0)
            {
                dieLine.Append($" {op} ");
                valueLine.Append($" {op} ");
            }

            var width = Math.Max(die.Length, value.Length);
            dieLine.Append(die.PadLeft(width));
            valueLine.Append(value.PadLeft(width));
        }

        // A single die on its own doesn't need "= total" repeating the same number
        if (columns.Count > 1)
            valueLine.Append($" = {roll.Value}");

        var droppedLine = dropped.Count > 0
            ? $"\n\ndropped: {string.Join(", ", dropped.Select(d => d.Value))}"
            : string.Empty;

        return $"```\n{dieLine.ToString().TrimEnd()}\n{valueLine}{droppedLine}\n```";
    }

    // "DiceTerm.d6" => "d6", "FudgeDiceTerm.dF" => "dF"
    private static string DieName(TermResult die) => die.Type[(die.Type.LastIndexOf('.') + 1)..];
}

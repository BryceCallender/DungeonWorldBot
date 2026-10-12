using System.Diagnostics.CodeAnalysis;
using System.Text;
using DiceNotation;

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
    /// Parses and rolls user supplied dice notation without throwing on bad input.
    /// </summary>
    public static bool TryRoll(IDiceParser parser, string expression, [NotNullWhen(true)] out DiceResult? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(expression))
            return false;

        try
        {
            result = parser.Parse(expression.Trim()).Roll();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string BuildDiceResult(DiceResult roll)
    {
        var total = roll.Value;
        var rollText = new StringBuilder();
        var rollRepresentation = new StringBuilder();
        var rollValues = new StringBuilder();
        
        foreach (var (index, rollResult) in roll.Results.Index())
        {
            rollRepresentation.Append(rollResult.Type);
            rollValues.Append(rollResult.Value);

            if (index >= roll.Results.Count - 1) 
                continue;
            
            rollRepresentation.Append(" + ");
            rollValues.Append(" + ");
        }

        rollText.AppendLine(rollRepresentation.ToString());
        rollText.AppendLine();
        if (roll.Results.Count == 1)
        {
            rollText.AppendLine($"{total}");
        }
        else
        {
            rollText.AppendLine($"{rollValues} = {total}");
        }

        return rollText.ToString();
    }
}
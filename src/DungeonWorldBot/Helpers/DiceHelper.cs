using System.Text;
using DiceNotation;

namespace DungeonWorldBot.Helpers;

public static class DiceHelper
{
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
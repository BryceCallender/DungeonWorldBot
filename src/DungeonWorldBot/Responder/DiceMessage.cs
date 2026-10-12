using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Responder;

/// <summary>
/// A roll requested by a chat message starting with '.', e.g. ".2d6+1 sneak attack" or ".dex+1 dodge the trap".
/// </summary>
/// <param name="Expression">Dice notation to roll, when this is a plain dice roll.</param>
/// <param name="Stat">The stat to roll 2d6 with, when this is a stat roll.</param>
/// <param name="Bonus">Extra modifier for a stat roll.</param>
/// <param name="Label">Optional text after the roll describing what it's for.</param>
public sealed partial record DiceMessage(string? Expression, StatType? Stat, int Bonus, string? Label)
{
    public const char Prefix = '.';

    // Keeps a single message from asking for millions of dice or absurd dice sizes
    public const int MaxDice = 100;
    public const int MaxFaces = 1000;

    [GeneratedRegex(@"^(str|dex|con|int|wis|cha)([+-]\d{1,2})?$", RegexOptions.IgnoreCase)]
    private static partial Regex StatRoll();

    // Characters DiceNotation understands (k = keep highest, e.g. 4d6k3); any other token starts the label
    [GeneratedRegex(@"^[0-9dDkK+\-]+$")]
    private static partial Regex DiceToken();

    [GeneratedRegex(@"(\d*)[dD](\d+)")]
    private static partial Regex DiceTerm();

    /// <returns>The requested roll, or null if the message isn't a roll (e.g. "..." or ".hello").</returns>
    public static DiceMessage? TryParse(string content)
    {
        if (content.Length < 2 || content[0] != Prefix)
            return null;

        var tokens = content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return null;

        var statMatch = StatRoll().Match(tokens[0]);
        if (statMatch.Success)
        {
            var stat = Enum.Parse<StatType>(statMatch.Groups[1].Value, ignoreCase: true);
            var bonus = statMatch.Groups[2].Success ? int.Parse(statMatch.Groups[2].Value) : 0;
            return new DiceMessage(null, stat, bonus, LabelFrom(tokens, 1));
        }

        var expressionLength = tokens.TakeWhile(t => DiceToken().IsMatch(t)).Count();
        if (expressionLength == 0)
            return null;

        var expression = string.Concat(tokens.Take(expressionLength));
        if (!IsReasonableRoll(expression))
            return null;

        return new DiceMessage(expression, null, 0, LabelFrom(tokens, expressionLength));
    }

    private static bool IsReasonableRoll(string expression)
    {
        var terms = DiceTerm().Matches(expression);

        // Something like ".5" or ".+1" has no dice in it
        if (terms.Count == 0)
            return false;

        var totalDice = 0;
        foreach (Match term in terms)
        {
            var count = term.Groups[1].Value.Length == 0 ? 1 : int.TryParse(term.Groups[1].Value, out var c) ? c : int.MaxValue;
            var faces = int.TryParse(term.Groups[2].Value, out var f) ? f : int.MaxValue;

            if (faces is < 2 or > MaxFaces)
                return false;

            totalDice += Math.Min(count, MaxDice + 1);
            if (totalDice > MaxDice)
                return false;
        }

        return true;
    }

    private static string? LabelFrom(string[] tokens, int start) =>
        start < tokens.Length ? string.Join(' ', tokens[start..]) : null;
}

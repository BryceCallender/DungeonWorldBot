using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rolling;

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

    [GeneratedRegex(@"^(str|dex|con|int|wis|cha)([+-]\d{1,2})?$", RegexOptions.IgnoreCase)]
    private static partial Regex StatRoll();

    // Characters dice notation can use (k/p/l keep or drop, ! explode, x and / math, f fudge, % percentile);
    // the first token with anything else starts the label
    [GeneratedRegex(@"^[0-9dDkKpPlLfF!%+\-xX/()]+$")]
    private static partial Regex DiceToken();

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

    // Something like ".5" or ".+1" has no dice in it, so it's just a chat message
    private static bool IsReasonableRoll(string expression) =>
        DiceRoller.IsReasonable(expression) && DiceRoller.CountDice(expression) > 0;

    private static string? LabelFrom(string[] tokens, int start) =>
        start < tokens.Length ? string.Join(' ', tokens[start..]) : null;
}

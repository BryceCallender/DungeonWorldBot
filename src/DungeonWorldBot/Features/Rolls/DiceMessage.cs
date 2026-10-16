using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// A roll requested by a chat message starting with '.', e.g. ".2d6+1 sneak attack" or ".dex+1 dodge the trap".
/// </summary>
/// <param name="Expression">Dice notation to roll, when this is a plain dice roll.</param>
/// <param name="Stat">The stat to roll 2d6 with, when this is a stat roll.</param>
/// <param name="Bonus">Extra modifier for a stat roll.</param>
/// <param name="Label">Optional text after the roll describing what it's for.</param>
/// <param name="Options">
/// A numbered list sent alongside the roll, e.g. "1 hatter 2 ziedrich 3 zephyr" above ".1d3", so the roll picks one.
/// </param>
public sealed partial record DiceMessage(
    string? Expression,
    StatType? Stat,
    int Bonus,
    string? Label,
    IReadOnlyDictionary<int, string>? Options = null)
{
    public const char Prefix = '.';

    [GeneratedRegex(@"^(str|dex|con|int|wis|cha)([+-]\d{1,2})?$", RegexOptions.IgnoreCase)]
    private static partial Regex StatRoll();

    // Characters dice notation can use (k/p/l keep or drop, ! explode, x and / math, f fudge, % percentile);
    // the first token with anything else starts the label
    [GeneratedRegex(@"^[0-9dDkKpPlLfF!%+\-xX/()]+$")]
    private static partial Regex DiceToken();

    // "1 hatter 2 ziedrich" or "1. hatter" / "2) ziedrich" on their own lines; each entry runs until the next number
    [GeneratedRegex(@"(?:^|\s)(\d{1,3})[.):]?\s+(.+?)(?=\s+\d{1,3}[.):]?\s|$)")]
    private static partial Regex OptionEntry();

    /// <summary>
    /// Finds the first line starting with '.' that's a roll; any numbered list on the other lines becomes its options.
    /// </summary>
    /// <returns>The requested roll, or null if the message isn't a roll (e.g. "..." or ".hello").</returns>
    public static DiceMessage? TryParse(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            var roll = TryParseLine(lines[i]);
            if (roll is not null)
                return roll with { Options = ParseOptions(lines.Where((_, j) => j != i)) };
        }

        return null;
    }

    private static DiceMessage? TryParseLine(string content)
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

    // Needs at least two entries so a stray number in chat isn't treated as a list
    private static IReadOnlyDictionary<int, string>? ParseOptions(IEnumerable<string> lines)
    {
        var options = new Dictionary<int, string>();
        foreach (var line in lines)
        foreach (Match match in OptionEntry().Matches(line))
        {
            var name = match.Groups[2].Value.Trim().TrimEnd(',', ';', '|').Trim();
            if (name.Length > 0)
                options.TryAdd(int.Parse(match.Groups[1].Value), name);
        }

        return options.Count >= 2 ? options : null;
    }

    private static string? LabelFrom(string[] tokens, int start) =>
        start < tokens.Length ? string.Join(' ', tokens[start..]) : null;
}

using System.Text.RegularExpressions;
using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// The two d6 of a move roll and its total with modifiers, which is all the roll stats need.
/// </summary>
public sealed partial record MoveDice(int First, int Second, int Total)
{
    public bool IsNatural2 => First == 1 && Second == 1;
    public bool IsNatural12 => First == 6 && Second == 6;
    public MoveOutcome Outcome => MoveRoller.OutcomeFor(Total);

    /// <returns>The move dice, or null if the roll isn't 2d6 plus flat modifiers (e.g. damage).</returns>
    public static MoveDice? From(DiceResult roll)
    {
        if (MoveRoller.OutcomeOf(roll) is null)
            return null;

        var dice = roll.Results.Where(d => d.AppliesToResultCalculation).ToList();
        return dice.Count == 2 ? new MoveDice(dice[0].Value, dice[1].Value, roll.Value) : null;
    }

    // Die names the bot has shown over the years: "d6" or "DiceTerm.d6", and flat modifiers like "1" or "constant"
    private const string Column = @"(?:[A-Za-z]+\.)*d(?:\d+|F|%)|\d+|constant";

    // "d6 + d6 + 1"
    [GeneratedRegex($@"^(?<name>{Column})(?:\s*[+-]\s*(?<name>{Column}))*$")]
    private static partial Regex DieNamesLine();

    // " 4 +  2 + 1 = 7", "6 + 4 + -1 = 9", or a lone "15" for one die
    [GeneratedRegex(@"^(?<value>-?\d+)(?:\s*[+-]\s*(?<value>-?\d+))*(?:\s*=\s*(?<total>-?\d+))?$")]
    private static partial Regex DieValuesLine();

    [GeneratedRegex(@"^(?:[A-Za-z]+\.)*d(?<faces>\d+|F|%)$")]
    private static partial Regex DieName();

    // 2022 rolls: "2d6+1", "Total Roll: 8", "(3+4)+(1)=8"
    [GeneratedRegex(@"^Total Roll:\s*(?<total>-?\d+)$")]
    private static partial Regex LegacyTotalLine();

    [GeneratedRegex(@"^(?:\((?<group>[^)]*)\)\+?)+=(?<total>-?\d+)$")]
    private static partial Regex LegacyValuesLine();

    [GeneratedRegex(@"^(?<count>\d*)d(?<faces>\d+)$")]
    private static partial Regex LegacyDiceTerm();

    /// <summary>
    /// Reads the move dice back out of a roll embed's description, in any format the bot has posted rolls in.
    /// </summary>
    /// <returns>The move dice, or null if the roll isn't a 2d6 roll or can't be read.</returns>
    public static MoveDice? FromDescription(string description)
    {
        var lines = description
            .Split('\n')
            .Select(l => l.Trim().Trim('`').Trim())
            .Where(l => l.Length > 0)
            .ToList();

        // Keeping two d6 out of more (e.g. "4d6k2") isn't a move roll
        if (lines.Any(l => l.StartsWith("dropped:")))
            return null;

        return FromLegacyLines(lines) ?? FromColumnLines(lines);
    }

    private static MoveDice? FromColumnLines(List<string> lines)
    {
        for (var i = 0; i + 1 < lines.Count; i++)
        {
            var names = DieNamesLine().Match(lines[i]);
            var values = DieValuesLine().Match(lines[i + 1]);
            if (!names.Success || !values.Success || names.Groups["name"].Captures.Count != values.Groups["value"].Captures.Count)
                continue;

            var faces = new List<string>();
            var rolled = new List<int>();
            foreach (var (name, value) in names.Groups["name"].Captures.Zip(values.Groups["value"].Captures))
            {
                if (DieName().Match(name.Value) is not { Success: true } die)
                    continue;

                faces.Add(die.Groups["faces"].Value);
                rolled.Add(int.Parse(value.Value));
            }

            var total = values.Groups["total"].Success
                ? int.Parse(values.Groups["total"].Value)
                : int.Parse(values.Groups["value"].Value);

            return IsTwoD6(faces) ? new MoveDice(rolled[0], rolled[1], total) : null;
        }

        return null;
    }

    private static MoveDice? FromLegacyLines(List<string> lines)
    {
        if (lines.Count < 3 || !LegacyTotalLine().IsMatch(lines[1]) || LegacyValuesLine().Match(lines[2]) is not { Success: true } values)
            return null;

        var terms = lines[0].ToLower().Replace("1d", "d").Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var groups = values.Groups["group"].Captures;
        if (terms.Length != groups.Count)
            return null;

        var faces = new List<string>();
        var rolled = new List<int>();
        foreach (var (term, group) in terms.Zip(groups))
        {
            if (LegacyDiceTerm().Match(term) is not { Success: true } dice)
                continue;

            foreach (var value in group.Value.Split('+', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(value, out var parsed))
                    return null;

                faces.Add(dice.Groups["faces"].Value);
                rolled.Add(parsed);
            }
        }

        return IsTwoD6(faces) ? new MoveDice(rolled[0], rolled[1], int.Parse(values.Groups["total"].Value)) : null;
    }

    private static bool IsTwoD6(List<string> faces) => faces is ["6", "6"];
}

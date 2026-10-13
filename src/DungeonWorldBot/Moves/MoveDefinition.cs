using System.Drawing;
using System.Text.RegularExpressions;
using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Helpers;

namespace DungeonWorldBot.Moves;

public enum MoveOutcome
{
    Miss,
    WeakHit,
    StrongHit,
}

/// <summary>
/// Which results a move's options apply on.
/// </summary>
[Flags]
public enum OptionBands
{
    StrongHit = 1,
    WeakHit = 2,
    Miss = 4,
    Hits = StrongHit | WeakHit,
    All = Hits | Miss,
}

/// <summary>
/// A Dungeon World move: roll 2d6 + modifier, then read the result by band (6-, 7-9, 10+).
/// </summary>
/// <param name="Stat">The stat the move always rolls with, or null when the player picks (Defy Danger) or it uses something else (Aid uses Bond).</param>
/// <param name="Options">Choices the player picks from on a hit, if the move has any.</param>
/// <param name="RollsDamage">Whether a hit deals the character's damage.</param>
/// <param name="OptionsOn">Which results the options apply on, e.g. only a 7-9 when they're costs (Volley).</param>
public sealed record MoveDefinition(
    string Name,
    StatType? Stat,
    string Trigger,
    string StrongHit,
    string WeakHit,
    string Miss,
    IReadOnlyList<string>? Options = null,
    bool RollsDamage = false,
    OptionBands OptionsOn = OptionBands.Hits)
{
    public const string DefaultMiss = "You fail.";

    public string Describe(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => StrongHit,
        MoveOutcome.WeakHit => WeakHit,
        _ => Miss,
    };

    public bool ShowsOptions(MoveOutcome outcome) => Options is { Count: > 0 } && OptionsOn.HasFlag(outcome switch
    {
        MoveOutcome.StrongHit => OptionBands.StrongHit,
        MoveOutcome.WeakHit => OptionBands.WeakHit,
        _ => OptionBands.Miss,
    });
}

public sealed record MoveRoll(DiceResult Dice, MoveOutcome Outcome);

public static partial class MoveRoller
{
    // 2d6 plus or minus flat modifiers, e.g. "2d6", "2d6+1", "2d6 - 1 + 2"
    [GeneratedRegex(@"^\s*2\s*d\s*6\s*([+-]\s*\d+\s*)*$", RegexOptions.IgnoreCase)]
    private static partial Regex MoveRollExpression();

    /// <summary>
    /// The 10+ / 7-9 / 6- band for a roll, if it's a 2d6 roll where bands mean something.
    /// </summary>
    public static MoveOutcome? OutcomeOf(DiceResult roll) =>
        MoveRollExpression().IsMatch(roll.DiceExpression) ? OutcomeFor(roll.Value) : null;

    /// <summary>
    /// What each band means for any roll that isn't a specific move.
    /// </summary>
    public static string GeneralMeaning(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => "You do it.",
        MoveOutcome.WeakHit => "You do it, but there's a cost, complication, or hard choice.",
        _ => "You fail.",
    };

    public static Color ColourFor(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => Color.Green,
        MoveOutcome.WeakHit => Color.Orange,
        _ => Color.Red,
    };

    public static MoveOutcome OutcomeFor(int total) => total switch
    {
        >= 10 => MoveOutcome.StrongHit,
        >= 7 => MoveOutcome.WeakHit,
        _ => MoveOutcome.Miss,
    };

    public static MoveRoll Roll(IDiceRoller dice, int modifier)
    {
        var result = dice.Roll(DiceHelper.AddModifier("2d6", modifier));
        return new MoveRoll(result, OutcomeFor(result.Value));
    }

    public static string Describe(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => "Strong Hit (10+)",
        MoveOutcome.WeakHit => "Weak Hit (7-9)",
        _ => "Miss (6-)",
    };
}

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
/// A Dungeon World move: roll 2d6 + modifier, then read the result by band (6-, 7-9, 10+).
/// </summary>
/// <param name="Stat">The stat the move always rolls with, or null when the player picks (Defy Danger) or it uses something else (Aid uses Bond).</param>
/// <param name="Options">Choices the player picks from on a hit, if the move has any.</param>
/// <param name="RollsDamage">Whether a hit deals the character's damage.</param>
public sealed record MoveDefinition(
    string Name,
    StatType? Stat,
    string Trigger,
    string StrongHit,
    string WeakHit,
    string Miss,
    IReadOnlyList<string>? Options = null,
    bool RollsDamage = false)
{
    public const string DefaultMiss = "Mark XP. The GM makes a move as hard as they like.";

    public string Describe(MoveOutcome outcome) => outcome switch
    {
        MoveOutcome.StrongHit => StrongHit,
        MoveOutcome.WeakHit => WeakHit,
        _ => Miss,
    };
}

public sealed record MoveRoll(DiceResult Dice, MoveOutcome Outcome);

public static class MoveRoller
{
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

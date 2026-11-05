using DungeonWorldBot.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using d20Tek.DiceNotation;
using d20Tek.DiceNotation.Results;

namespace DungeonWorldBot.Features.Rolls;

public interface IDiceRoller
{
    /// <summary>
    /// Rolls user supplied dice notation, rejecting anything invalid or unreasonably large instead of throwing.
    /// </summary>
    bool TryRoll(string expression, [NotNullWhen(true)] out DiceResult? result);

    /// <summary>
    /// Rolls dice notation that's known to be valid, e.g. "2d6+1" built by the bot itself.
    /// </summary>
    DiceResult Roll(string expression);
}

[Singleton<IDiceRoller>]
public partial class DiceRoller : IDiceRoller
{
    // Keeps a single roll from asking for millions of dice or absurd dice sizes
    public const int MaxDice = 100;
    public const int MaxFaces = 1000;
    public const int MaxExpressionLength = 100;

    private readonly IDieRoller _dieRoller;

    public DiceRoller() : this(new SecureDieRoller())
    {
    }

    /// <param name="dieRoller">The source of each die result; tests can pass a ConstantDieRoller.</param>
    public DiceRoller(IDieRoller dieRoller)
    {
        _dieRoller = dieRoller;
    }

    [GeneratedRegex(@"(\d*)\s*[dD]\s*(\d+|%)")]
    private static partial Regex DiceTerm();

    [GeneratedRegex(@"(\d*)\s*[fF]")]
    private static partial Regex FudgeTerm();

    [GeneratedRegex(@"([dD]\s*(\d+))\s*!(?!\s*\d)")]
    private static partial Regex BareExplode();

    /// <summary>
    /// Works around d20tek-dicenotation 5.1.7 using the number of dice as the default explode threshold
    /// (so "2d6!" explodes on 2+ and "1d6!" explodes forever) by spelling out the die's maximum: "2d6!" => "2d6!6".
    /// </summary>
    public static string NormalizeExploding(string expression) =>
        BareExplode().Replace(expression, m => $"{m.Groups[1].Value}!{m.Groups[2].Value}");

    public bool TryRoll(string expression, [NotNullWhen(true)] out DiceResult? result)
    {
        result = null;
        if (!IsReasonable(expression))
            return false;

        result = RollUnchecked(expression.Trim());
        if (result.HasError)
        {
            result = null;
            return false;
        }

        return true;
    }

    public DiceResult Roll(string expression)
    {
        var result = RollUnchecked(expression);
        return result.HasError
            ? throw new ArgumentException($"Invalid dice expression '{expression}': {result.Error}", nameof(expression))
            : result;
    }

    private DiceResult RollUnchecked(string expression)
    {
        // A fresh Dice per roll keeps parsing state out of any concurrently handled messages.
        // Unbounded, so "2d6-3" can really total less than 1 (the library clamps to 1 by default).
        var configuration = new DiceConfiguration();
        configuration.SetHasBoundedResult(false);

        return new Dice(configuration).Roll(NormalizeExploding(expression), _dieRoller);
    }

    /// <summary>
    /// Whether the expression stays within the length, dice count, and dice size limits.
    /// Flat numbers like "4" are fine (e.g. flat damage).
    /// </summary>
    public static bool IsReasonable(string expression) =>
        !string.IsNullOrWhiteSpace(expression)
        && expression.Length <= MaxExpressionLength
        && CountDice(expression) is >= 0 and <= MaxDice;

    /// <returns>How many dice the expression rolls, or -1 if a die has an invalid number of faces.</returns>
    public static int CountDice(string expression)
    {
        var totalDice = 0;

        foreach (Match term in DiceTerm().Matches(expression))
        {
            var faces = term.Groups[2].Value == "%" ? 100 : ParseOrMax(term.Groups[2].Value);
            if (faces is < 1 or > MaxFaces)
                return -1;

            totalDice = AddCapped(totalDice, CountOf(term));
        }

        foreach (Match term in FudgeTerm().Matches(expression))
        {
            totalDice = AddCapped(totalDice, CountOf(term));
        }

        return totalDice;
    }

    // Saturates just past the limit so huge counts can't overflow
    private static int AddCapped(int total, int count) => Math.Min(total + Math.Min(count, MaxDice + 1), MaxDice + 1);

    private static int CountOf(Match term) => term.Groups[1].Value.Length == 0 ? 1 : ParseOrMax(term.Groups[1].Value);

    private static int ParseOrMax(string value) => int.TryParse(value, out var parsed) ? parsed : int.MaxValue;
}

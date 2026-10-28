using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.GrimWorld;
using DungeonWorldBot.Features.Moves;

namespace DungeonWorldBot.Rules;

/// <summary>
/// One of the Battlemaster's Tactics, e.g. Cautious: "+1 to Defy Danger."
/// </summary>
public record Tactic(string Name, string Effect);

/// <summary>
/// The Battlemaster's Tactics: which one is active, and what it does to their rolls and damage.
/// </summary>
public static class TacticRules
{
    public const string Tactics = "Tactics";
    public const string ChangeTactics = "Change Tactics";
    public const string Counterattack = "Counterattack";
    public const string DefaultTacticPrefix = "Default Tactic: ";
    public const string RecklessAggression = "Reckless Aggression";

    /// <summary>
    /// The ability whose description holds the active Tactic's name. Without it, the Default Tactic is active.
    /// </summary>
    public const string ActiveTactic = "Active Tactic";

    public const string RecklessDamageDice = "1d4";

    public static readonly Tactic Aggressive = new("Aggressive", "Hack and Slash deals your damage even on a 6-.");
    public static readonly Tactic Cautious = new("Cautious", "+1 to Defy Danger.");
    public static readonly Tactic Reactive = new("Reactive", "You can't Hack and Slash, but you can Counterattack.");
    public static readonly Tactic Defensive = new("Defensive", "When you Defend, a 6- counts as a 7-9.");
    public static readonly Tactic Reckless = new("Reckless", "Deal +1d4 damage, and take +1d4 damage.");

    public static readonly IReadOnlyList<Tactic> All = [Aggressive, Cautious, Reactive, Defensive, Reckless];

    public static ClassResource Gambit => Battlemaster.Sheet.FindResource("Gambit")!;

    public static Tactic? Find(string? name) =>
        name is null ? null : All.FirstOrDefault(t => t.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool Knows(Character character, string move) =>
        ClassSheetLibrary.KnownMoves(character).Contains(move, StringComparer.OrdinalIgnoreCase);

    public static bool UsesTactics(Character character) => Knows(character, Tactics);

    /// <summary>
    /// The Tactic chosen as the character's Default Tactic, if they've picked one.
    /// </summary>
    public static Tactic? Default(Character character) =>
        character.Abilities?
            .Where(a => a.Name.StartsWith(DefaultTacticPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(a => Find(a.Name[DefaultTacticPrefix.Length..]))
            .FirstOrDefault(t => t is not null);

    /// <summary>
    /// The Tactic the character is in: the last one they changed to, or their Default Tactic.
    /// </summary>
    public static Tactic? Current(Character character)
    {
        if (!UsesTactics(character))
            return null;

        var active = character.Abilities?.FirstOrDefault(a => a.Name.Equals(ActiveTactic, StringComparison.OrdinalIgnoreCase));
        return Find(active?.Description) ?? Default(character);
    }

    public static bool IsIn(Character character, Tactic tactic) => Current(character) == tactic;

    /// <summary>
    /// Why the move can't be made in the character's current Tactic, or null if it can.
    /// </summary>
    public static string? WhyCannotMake(Character character, string move)
    {
        if (!UsesTactics(character))
            return null;

        if (move == BasicMoves.HackAndSlash.Name && IsIn(character, Reactive))
            return $"{character.Name} is in Reactive Tactics and can't Hack and Slash. Counterattack with /move class, or change Tactics with /tactic change";

        if (move == Counterattack && !IsIn(character, Reactive))
            return $"{character.Name} can only Counterattack in Reactive Tactics. Change Tactics with /tactic change";

        return null;
    }

    /// <summary>
    /// What the current Tactic adds to a move's roll, like Cautious's +1 to Defy Danger.
    /// </summary>
    public static int RollBonus(Character character, string move) =>
        move == BasicMoves.DefyDanger.Name && IsIn(character, Cautious) ? 1 : 0;

    /// <summary>
    /// The outcome after the current Tactic, e.g. a missed Defend counts as a 7-9 in Defensive Tactics.
    /// </summary>
    public static MoveOutcome Outcome(Character character, string move, MoveOutcome outcome) =>
        outcome == MoveOutcome.Miss && move == BasicMoves.Defend.Name && IsIn(character, Defensive)
            ? MoveOutcome.WeakHit
            : outcome;

    /// <summary>
    /// Whether a move deals damage on a miss, like Hack and Slash in Aggressive Tactics.
    /// </summary>
    public static bool DamagesOnMiss(Character character, string move) =>
        move == BasicMoves.HackAndSlash.Name && IsIn(character, Aggressive);

    /// <summary>
    /// Extra dice the character deals or takes in their current Tactic, e.g. "1d4" in Reckless Tactics.
    /// </summary>
    public static string? ExtraDamage(Character character) =>
        IsIn(character, Reckless) ? RecklessDamageDice : null;

    /// <summary>
    /// Whether the character rolls damage twice and keeps the better, from Reckless Aggression in Aggressive Tactics.
    /// </summary>
    public static bool RollsDamageTwice(Character character) =>
        IsIn(character, Aggressive) && Knows(character, RecklessAggression);

    /// <summary>
    /// Reminders from the character's advanced moves that work in their current Tactic.
    /// </summary>
    public static IEnumerable<string> Reminders(Character character)
    {
        var tactic = Current(character);
        if (tactic == Reckless && Knows(character, RecklessAggression))
            yield return $"**{RecklessAggression}:** your melee attacks gain +2 piercing.";
        if (RollsDamageTwice(character))
            yield return $"**{RecklessAggression}:** your damage is rolled twice, keeping the better result.";
        if (tactic == Cautious && Knows(character, "Combat Sight"))
            yield return "**Combat Sight:** a 10+ on Defy Danger gains you a Gambit.";
        if (tactic == Reactive && Knows(character, "Combat Sight"))
            yield return "**Combat Sight:** a 10+ on Counterattack lets you ask the GM one Discern Realities question.";
        if (tactic == Reactive && Knows(character, "Block and Strike"))
            yield return "**Block and Strike:** with a shield, Counterattack can roll +STR instead.";
        if (tactic == Defensive && Knows(character, "Intelligent Defense"))
            yield return "**Intelligent Defense:** spend 1 Gambit to halve an attack's damage or effect on you or what you're Defending.";
    }

    /// <summary>
    /// One line for the character sheet, e.g. "⚔️ **Cautious** (default): +1 to Defy Danger."
    /// </summary>
    public static string Status(Character character) => Current(character) is { } tactic
        ? $"⚔️ **{tactic.Name}**{(tactic == Default(character) ? " (default)" : string.Empty)}: {tactic.Effect}"
        : "No Tactic yet. Pick your Default Tactic with /class learn";
}

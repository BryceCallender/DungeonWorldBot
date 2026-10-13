using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Moves;
using static DungeonWorldBot.ClassSheets.ClassMove;

namespace DungeonWorldBot.Subclasses.Homebrew;

/// <summary>
/// Tactical Guardian: a homebrew subclass for any class, fused with light. Reads the room for a strategy pool of d6s,
/// spends them to change other people's rolls, and holds the line for the party.
/// </summary>
public static class TacticalGuardian
{
    public const string Name = "Tactical Guardian";
    public const string ReadTheRoom = "Read the Room";
    public const string OneStepAhead = "One Step Ahead";
    public const string FlashstepExchange = "Flashstep Exchange";
    public const string GuardiansStand = "Guardian's Stand";
    public const string ShieldedPursuit = "Shielded Pursuit";
    public const string PerfectedAssault = "Perfected Assault";

    /// <summary>
    /// The ability holding the d6s in the character's strategy pool.
    /// </summary>
    public const string StrategyPool = "Strategy Pool";

    /// <summary>
    /// The ability holding the damage Shielded Pursuit has stored.
    /// </summary>
    public const string StoredDamage = "Stored Damage";

    /// <summary>
    /// The most d6s Read the Room gives, so a huge bonus can't flood the embed.
    /// </summary>
    public const int MaxPool = 20;

    // Read the Room and Perfected Assault are rolled with /guardian, which tracks the pool and the short-rest use, so
    // they're kept off /move class by dropping their Roll. These keep the definitions for the command.
    private static readonly ClassMove ReadTheRoomMove = Rolled(ReadTheRoom, StatType.Int,
        "When a situation is about to turn violent",
        "You gain a strategy pool: roll a number of d6 equal to your roll. Also ask the GM one strategic question; they'll answer with something true and useful. Explain how you deduce it.",
        "You gain a strategy pool: roll a number of d6 equal to your roll. The GM tells you something true, but it's up to you to interpret and make use of it.",
        "You gain a strategy pool: roll a number of d6 equal to your roll. But you're on your own, and may not have gathered enough to act decisively.",
        note: "Use /guardian read-the-room");

    private static readonly ClassMove PerfectedAssaultMove = Rolled(PerfectedAssault, StatType.Int,
        "When you declare a Perfected Assault, an orchestrated strike where every movement is part of the plan",
        "You and all allies near you each make a single attack with full damage, regardless of weapon or attack type. Enemies are disoriented (-1 ongoing). Each ally may also immediately take a free action (attack, defend, or move) without rolling.",
        "Your strategy lands, but everyone's damage is halved and there's no disorientation. Your allies take +1 forward, but get no free action.",
        "The attack falls apart: your allies hesitate or misfire. Every attack in the assault deals half damage and takes -1 forward. One enemy may immediately attack you or your team, and that attack can't be avoided or reduced by armor.",
        note: "Once used, you can't use it again until after a short rest. Use /guardian perfected-assault, and restore it with /ability restore");

    public static MoveDefinition ReadTheRoomRoll => ReadTheRoomMove.Roll!;
    public static MoveDefinition PerfectedAssaultRoll => PerfectedAssaultMove.Roll!;

    public static readonly Subclass Subclass = new(
        Name,
        "Fused with Light",
        "Homebrew",
        [],
        "Your fusion with light sharpens your mind and speeds your reactions. Read the room before the fight to build a strategy pool, " +
        "bend the odds one die at a time, and stand in the breach for your allies.",
        [
            new(1, ReadTheRoomMove with { Roll = null }),
            new(1, new(OneStepAhead,
                "Your fusion with light speeds your reactions and thoughts. During combat, when an NPC or another player acts (never on your own " +
                "turn), replace one die from their roll with a die from your strategy pool. Discard it from the pool and explain plausibly how you " +
                "influence the encounter, possibly with an ally's help. It can't alter Last Breath, world-level random checks, or similar rolls. " +
                "Use /guardian one-step-ahead")),
            new(1, Rolled(FlashstepExchange, StatType.Int,
                "When you swap places with someone, friend or foe, by bending space and perception with your fused light",
                "You switch places seamlessly, despite obstacles, distance (near or far), or awareness. Choose one: catch them off guard (they take -1 forward); land in perfect position (take +1 forward); time it perfectly (an ally may act at once with a surprise advantage).",
                "The swap works, imperfectly. Choose one: you or your target are exposed (the enemy takes +1 forward against you or them); the light burns hot (take 1d4 damage, ignoring armor); you or your target land somewhere compromised (the GM says how).",
                "The light bends wrong. The GM chooses one: you end up in the wrong place, maybe in danger; your light must recharge before you use this again; the enemy notices and acts immediately or takes +1 forward.",
                note: "You must see your target, even briefly, or have a direct line of light to them. Not in total darkness without another light source.")),
            new(3, Rolled(GuardiansStand, StatType.Con,
                "When you defend an ally or group of allies and declare that you're standing in the breach",
                "Hold the line as long as needed. Choose one: allies within near take +1 forward; raise cover (a shield wall or barrier) giving you and allies there +2 armor until the end of next round; make a light doppelganger to draw enemy aggression.",
                "You hold, at a cost. Choose one: your position is compromised (the next enemy attack on your side deals +1 damage or adds a debuff); you're exhausted (take -1 forward); an ally you protect falters (they take -1 forward).",
                "Your defense falters. The GM chooses one: you're overwhelmed (the next attack on your side deals +1 damage, and you're knocked down: -1 forward); your stance weakens (bonus armor drops to 0, ending this round); you leave an opening (an enemy takes +1 forward against your side).",
                note: "For the fight: +1 magical armor (stacks), and you can take a protected ally's damage instead, halved (rounded down). Use /guardian take-hit")),
            new(6, new(ShieldedPursuit,
                "(Passive) You're not just a wall, but a shield and a spear. When you take damage while defending, you may store half of it " +
                "(rounded down). When you make an attack roll, you can release the stored damage on top of your normal damage, or save it for a " +
                "later attack. Stored damage can only be released while the battle is happening. Use /guardian take-hit to store it and " +
                "/guardian release to unleash it")),
            new(9, PerfectedAssaultMove with { Roll = null, Uses = 1 }),
        ])
    {
        Status = Describe,
        StateAbilities = [StrategyPool, StoredDamage],
        CommandHint = "Use /guardian for Read the Room, your strategy pool, stored damage, and Perfected Assault. Roll Flashstep Exchange and Guardian's Stand with /move class.",
    };

    /// <summary>
    /// The d6s in the character's strategy pool.
    /// </summary>
    public static IReadOnlyList<int> PoolOf(Character character) =>
        ParsePool(Find(character, StrategyPool)?.Description);

    public static IReadOnlyList<int> ParsePool(string? stored) =>
        (stored ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(d => int.TryParse(d, out var value) ? value : 0)
            .Where(d => d is >= 1 and <= 6)
            .ToList();

    /// <summary>
    /// How a pool is written into the Strategy Pool ability's description.
    /// </summary>
    public static string StorePool(IEnumerable<int> pool) => string.Join(", ", pool);

    /// <summary>
    /// The pool without one die showing the value, or null if no die shows it.
    /// </summary>
    public static IReadOnlyList<int>? Spend(IReadOnlyList<int> pool, int value)
    {
        var index = pool.ToList().IndexOf(value);
        if (index < 0)
            return null;

        return pool.Where((_, i) => i != index).ToList();
    }

    /// <summary>
    /// How many d6s Read the Room gives for a roll's total.
    /// </summary>
    public static int PoolSize(int total) => Math.Clamp(total, 0, MaxPool);

    public static int StoredDamageOf(Character character) =>
        int.TryParse(Find(character, StoredDamage)?.Description, out var stored) ? Math.Max(0, stored) : 0;

    /// <summary>
    /// What Shielded Pursuit stores from damage taken while defending.
    /// </summary>
    public static int Stores(int damageTaken) => Math.Max(0, damageTaken) / 2;

    /// <summary>
    /// The damage the Guardian takes for a hit, halved when they take it in an ally's place with Guardian's Stand.
    /// </summary>
    public static int DamageTaken(int damage, bool forAlly) => forAlly ? Math.Max(0, damage) / 2 : Math.Max(0, damage);

    public static string FormatPool(IReadOnlyList<int> pool) =>
        pool.Count > 0 ? string.Join(" ", pool.OrderByDescending(d => d).Select(d => $"`{d}`")) : "empty";

    private static string? Describe(Character character)
    {
        var parts = new List<string>();
        if (PoolOf(character) is { Count: > 0 } pool)
            parts.Add($"**Strategy pool:** {FormatPool(pool)}");
        if (StoredDamageOf(character) is > 0 and var stored)
            parts.Add($"**Stored damage:** {stored}");

        return parts.Count > 0 ? string.Join('\n', parts) : null;
    }

    private static Ability? Find(Character character, string name) =>
        character.Abilities?.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

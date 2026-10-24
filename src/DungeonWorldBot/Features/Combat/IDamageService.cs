using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Combat;

/// <summary>
/// A character's damage roll, with everything that changed it.
/// </summary>
/// <param name="Expression">What was rolled, e.g. "d8+6".</param>
/// <param name="Total">The damage dealt, after gear like a Sapphire reroll.</param>
public record DamageRoll(string Expression, DiceResult Dice, int Total, IReadOnlyList<RollBonus> Bonuses, GearDamage Gear)
{
    /// <summary>
    /// Lines explaining the bonuses and gear effects, each starting with a newline, or empty.
    /// </summary>
    public string Details =>
        (Bonuses.Count > 0 ? $"\nIncludes {string.Join(", ", Bonuses.Select(b => $"{b.Source} {(b.Amount >= 0 ? "+" : "")}{b.Amount}"))}" : string.Empty)
        + string.Concat(Gear.Notes.Select(n => $"\n{n}"));
}

public interface IDamageService
{
    /// <summary>
    /// Rolls the character's damage for a move, with subclass bonuses (like a Boon of Wrath) and their gear's effects.
    /// </summary>
    /// <param name="extra">Flat damage the player adds for something the bot doesn't track.</param>
    /// <returns>The roll, or null if the character's damage dice can't be rolled.</returns>
    Task<DamageRoll?> RollAsync(Character character, string move, int extra = 0);

    /// <summary>
    /// The character's gear's effects on damage they rolled some other way, like Lunar Barrage's d10s.
    /// </summary>
    Task<GearDamage> ApplyGearAsync(Character character, string move, IReadOnlyList<(int Sides, int Value)> dice);

    /// <summary>
    /// Subclass bonuses to a roll, from the character and the rest of the party.
    /// </summary>
    Task<IReadOnlyList<RollBonus>> BonusesAsync(Character character, RollKind kind, StatType? stat = null);

    /// <summary>
    /// Everything added to the character's damage, each with where it came from: their gear's damage rating and subclass
    /// bonuses from them and the party, plus any extra damage the player adds for something the bot doesn't track.
    /// </summary>
    Task<IReadOnlyList<RollBonus>> DamageBonusesAsync(Character character, int extra = 0);

    /// <summary>
    /// Hits the character, after their armor against its type. A Battlemaster in Reckless Tactics takes +1d4.
    /// </summary>
    Task<PlayerHit> TakeAsync(Character character, int amount, DamageType type, int piercing = 0);
}

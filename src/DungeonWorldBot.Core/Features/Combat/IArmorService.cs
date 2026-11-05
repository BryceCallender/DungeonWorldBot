using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Combat;

public interface IArmorService
{
    /// <summary>
    /// Takes the hit off the character's HP after their armor against its type, and breaks any armor that only lasted
    /// until they were hit.
    /// </summary>
    Task<DamageTaken> TakeDamageAsync(Character character, int damage, DamageType type, int piercing = 0);

    /// <summary>
    /// Sets the character's own armor of this type, e.g. after changing gear.
    /// </summary>
    Task SetArmorAsync(Character character, ArmorType type, int value);

    Task AddBonusAsync(Character character, string source, ArmorType type, int amount, ArmorDuration duration);

    /// <summary>
    /// Gives each character the same temporary armor, like a barrier around the party, saved together.
    /// </summary>
    Task AddBonusAsync(IEnumerable<Character> characters, string source, ArmorType type, int amount, ArmorDuration duration);

    /// <summary>
    /// Removes temporary armor from this source, or all of it when no source is given.
    /// </summary>
    /// <returns>The armor removed.</returns>
    Task<IReadOnlyList<ArmorBonus>> RemoveBonusesAsync(Character character, string? source = null);

    /// <summary>
    /// Removes temporary armor from this source from each character, saved together.
    /// </summary>
    Task RemoveBonusesAsync(IEnumerable<Character> characters, string source);

    /// <summary>
    /// Removes temporary armor that only lasts until the fight ends or until the character is hit.
    /// </summary>
    /// <returns>The armor removed.</returns>
    Task<IReadOnlyList<ArmorBonus>> EndFightAsync(Character character);

    /// <summary>
    /// Ends the fight for every character, as <see cref="EndFightAsync"/> does for one.
    /// </summary>
    /// <returns>Each character who lost temporary armor, with what they lost.</returns>
    Task<IReadOnlyList<ClearedArmor>> EndFightForAllAsync();

    /// <summary>
    /// Ends the fight for some characters, as <see cref="EndFightAsync"/> does for one.
    /// </summary>
    /// <param name="characters">Their players' Discord IDs.</param>
    /// <returns>Each character who lost temporary armor, with what they lost.</returns>
    Task<IReadOnlyList<ClearedArmor>> EndFightForAsync(IReadOnlyCollection<Snowflake> characters);
}

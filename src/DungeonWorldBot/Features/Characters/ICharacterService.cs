using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

public interface ICharacterService
{
    Task<Character?> GetCharacterFromUserAsync(Snowflake userId);

    Task<List<Character>> GetCharactersAsync();

    /// <summary>
    /// Every character's name and class, without the rest of the sheet, for suggestions that have to answer quickly.
    /// </summary>
    Task<List<CharacterName>> GetCharacterNamesAsync();

    Task AddCharacterAsync(Character character);

    Task HealAsync(Character character, int amount);

    Task FullHealAsync(Character character);

    Task DamageAsync(Character character, int amount);

    Task BondWithAsync(Character character, Character bonder);

    Task UpdateCharacterStatAsync(Character character, StatType statType, int value);

    Task SetLevelAsync(Character character, int level);

    Task UpdateCharacterDamageModifierAsync(Character character, string modifier);

    Task AdjustArmorAsync(Character character, int amount);
}

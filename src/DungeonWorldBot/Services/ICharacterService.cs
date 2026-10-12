using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Services;

public interface ICharacterService
{
    Task<Character?> GetCharacterFromUserAsync(Snowflake userId);

    Task<List<Character>> GetCharactersAsync();

    Task AddCharacterAsync(Character character);

    Task HealAsync(Character character, int amount);

    Task FullHealAsync(Character character);

    Task DamageAsync(Character character, int amount);

    Task BondWithAsync(Character character, Character bonder);

    Task UpdateCharacterStatAsync(Character character, StatType statType, int value);

    Task SetLevelAsync(Character character, int level);

    Task UpdateCharacterDamageModifierAsync(Character character, string modifier);
}

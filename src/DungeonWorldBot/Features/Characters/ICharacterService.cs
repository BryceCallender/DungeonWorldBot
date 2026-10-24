using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

public interface ICharacterService
{
    /// <summary>
    /// The character the player is playing: their own, or one they run for someone else while it's in play.
    /// </summary>
    Task<Character?> GetCharacterFromUserAsync(Snowflake userId);

    /// <summary>
    /// A character by their own ID, whoever is playing them.
    /// </summary>
    Task<Character?> GetCharacterAsync(Snowflake characterId);

    /// <summary>
    /// The characters the player runs alongside their own.
    /// </summary>
    Task<List<CharacterName>> GetControlledCharacterNamesAsync(Snowflake userId);

    /// <summary>
    /// Puts one of the characters the player runs in play, or with no name, takes them all out so the player is back
    /// to their own.
    /// </summary>
    Task PlayAsAsync(Snowflake userId, string? name);

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

using DungeonWorldBot.Data.Entities;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

public interface ICharacterService
{
    Task<Character?> GetCharacterFromUserAsync(Snowflake userId);

    /// <summary>
    /// The user's whole character sheet for showing it. Not tracked, so it's quicker to load but changes to it aren't
    /// saved: use <see cref="GetCharacterFromUserAsync"/> for anything that changes the character.
    /// </summary>
    Task<Character?> GetCharacterToShowAsync(Snowflake userId);

    /// <summary>
    /// The whole sheet as <see cref="GetCharacterToShowAsync"/> loads it, plus the goals and notes the player keeps in
    /// their journal, which nothing else loads since no rule reads them.
    /// </summary>
    Task<Character?> GetCharacterWithJournalAsync(Snowflake userId);

    /// <summary>
    /// The user's character with only their class and abilities loaded, which is all it takes to know their moves.
    /// Not tracked, so changes to it aren't saved. Quick enough for suggestions while they type.
    /// </summary>
    Task<Character?> GetCharacterMovesAsync(Snowflake userId);

    Task<List<Character>> GetCharactersAsync();

    /// <summary>
    /// Every character's name and class, without the rest of the sheet, for suggestions that have to answer quickly.
    /// </summary>
    Task<List<CharacterName>> GetCharacterNamesAsync();

    /// <summary>
    /// The name of the user's character, without loading the rest of the sheet.
    /// </summary>
    Task<string?> GetCharacterNameAsync(Snowflake userId);

    /// <summary>
    /// Every character with only their abilities loaded, which is all subclass roll bonuses read. Not tracked, so
    /// changes to them aren't saved.
    /// </summary>
    Task<List<Character>> GetCharacterAbilitiesAsync();

    /// <summary>
    /// Has the user play the character until they release it: the character's sheet stands in for their own in the
    /// commands they run. Whoever they were playing before is let go.
    /// </summary>
    Task TakeOverAsync(Snowflake characterId, Snowflake userId);

    /// <summary>
    /// Hands the character back, so nobody else is playing it.
    /// </summary>
    Task ReleaseAsync(Snowflake characterId);

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

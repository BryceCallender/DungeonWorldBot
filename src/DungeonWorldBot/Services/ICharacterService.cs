using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Services;

public interface ICharacterService
{
    Task<Character?> GetCharacterFromUserAsync(IUser user);

    Task AddCharacterAsync(Character character);
    
    List<Character> GetCharacters();

    Task ChangeCharacterAlignment(Character character, Alignment alignment);

    Task LevelUp(List<Character> characters);

    Task Heal(Character user, int amount);

    Task Damage(Character user, int amount);

    Task BondWith(Character user, Character bonder);
}
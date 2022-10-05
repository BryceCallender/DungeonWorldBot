using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;

namespace DungeonWorldBot.Services;

public interface ICharacterService
{
    Task<Character?> GetCharacterFromUserAsync(IUser user);

    Task AddCharacterAsync(string name);
    List<Character> GetCharacters();
}
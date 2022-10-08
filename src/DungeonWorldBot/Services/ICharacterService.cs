using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Services;

public interface ICharacterService
{
    Task<Character?> GetCharacterFromUserAsync(IUser user);

    Task AddCharacterAsync(Snowflake userID, string name);
    List<Character> GetCharacters();
}
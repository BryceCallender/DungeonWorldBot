using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Discord.API.Abstractions.Objects;

namespace DungeonWorldBot.Services.Implementation;

public class CharacterService : ICharacterService
{
    private readonly DungeonWorldContext _dungeonWorldContext;
    
    public CharacterService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    public async Task<Character?> GetCharacterFromUserAsync(IUser user)
    {
        return await _dungeonWorldContext.Characters.FirstOrDefaultAsync(c => c.ID == user.ID);
    }

    public async Task AddCharacterAsync(string name)
    {
        _dungeonWorldContext.Characters.Add(new Character
        {
            Name = name
        });

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public List<Character> GetCharacters()
    {
        return _dungeonWorldContext.Characters.ToList();
    }
}
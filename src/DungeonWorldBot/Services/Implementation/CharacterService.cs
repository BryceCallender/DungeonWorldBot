using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

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
        return await _dungeonWorldContext.Characters
            .Include(c => c.Class)
            .Include(c => c.Health)
            .Include(c => c.Stats)
            .FirstOrDefaultAsync(c => c.ID == user.ID);
    }

    public async Task AddCharacterAsync(Character character)
    {
        _dungeonWorldContext.Characters.Add(character);
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task ChangeCharacterAlignment(Character character, Alignment alignment)
    {
        character.Alignment = alignment;
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task LevelUp(List<Character> characters)
    {
        foreach (var character in characters)
        {
            character.Level++;
        }

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public List<Character> GetCharacters()
    {
        return _dungeonWorldContext.Characters
            .Include(c => c.Class)
            .Include(c => c.Health)
            .Include(c => c.Stats)
            .ToList();
    }
}
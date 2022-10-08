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

    public async Task AddCharacterAsync(Snowflake userID, string name)
    {
        _dungeonWorldContext.Characters.Add(new Character
        {
            ID = userID,
            Name = "Zephyr",
            Alignment = Alignment.LawfulGood,
            ArmorRating = 0,
            Class = new Class
            {
                Type = ClassType.Ranger
            },
            Health = new Health
            {
                CurrentHP = 10,
                MaxHP = 10
            },
            Level = 1,
            Stats = new List<Stat>
            {
                new() { StatType = StatType.Dex, Value = 16 },
                new() { StatType = StatType.Cha, Value = 15 },
                new() { StatType = StatType.Con, Value = 13 },
                new() { StatType = StatType.Str, Value = 12 },
                new() { StatType = StatType.Wis, Value = 9  },
                new() { StatType = StatType.Int, Value = 8  }
            }
        });

        try
        {
            await _dungeonWorldContext.SaveChangesAsync();
           
        }
        catch (Exception)
        {
            var a = 1;
        }
       
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
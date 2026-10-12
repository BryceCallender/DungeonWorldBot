using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Services.Implementation;

public class CharacterService : ICharacterService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public CharacterService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    private IQueryable<Character> CharactersWithDetails =>
        _dungeonWorldContext.Characters
            .Include(c => c.Class)
            .Include(c => c.Health)
            .Include(c => c.Stats)
            .Include(c => c.Bonds)
            .Include(c => c.Abilities)
            .Include(c => c.Resources)
            .Include(c => c.Inventory)
            .ThenInclude(i => i.Items);

    public Task<Character?> GetCharacterFromUserAsync(Snowflake userId)
    {
        return CharactersWithDetails.FirstOrDefaultAsync(c => c.ID == userId);
    }

    public Task<List<Character>> GetCharactersAsync()
    {
        return CharactersWithDetails.ToListAsync();
    }

    public async Task AddCharacterAsync(Character character)
    {
        _dungeonWorldContext.Characters.Add(character);
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task BondWithAsync(Character character, Character bonder)
    {
        character.Bonds ??= new List<Bond>();
        character.Bonds.Add(new Bond { TargetID = bonder.ID, TargetName = bonder.Name });

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task DamageAsync(Character character, int amount)
    {
        character.Health.CurrentHP = Math.Max(0, character.Health.CurrentHP - amount);
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task HealAsync(Character character, int amount)
    {
        character.Health.CurrentHP = Math.Min(character.Health.MaxHP, character.Health.CurrentHP + amount);
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task FullHealAsync(Character character)
    {
        character.Health.CurrentHP = character.Health.MaxHP;
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task UpdateCharacterStatAsync(Character character, StatType statType, int value)
    {
        var stat = character.Stats?.FirstOrDefault(s => s.StatType == statType);
        if (stat != null)
        {
            stat.Value = value;
        }

        // Max HP is derived from constitution
        if (statType == StatType.Con)
        {
            character.Health.MaxHP = character.Class.HealthModifier + value;
            character.Health.CurrentHP = Math.Min(character.Health.CurrentHP, character.Health.MaxHP);
        }

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task UpdateCharacterDamageModifierAsync(Character character, string modifier)
    {
        character.DamageModifier = modifier;
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task AdjustArmorAsync(Character character, int amount)
    {
        character.ArmorRating += amount;
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task SetLevelAsync(Character character, int level)
    {
        character.Level = level;
        await _dungeonWorldContext.SaveChangesAsync();
    }
}

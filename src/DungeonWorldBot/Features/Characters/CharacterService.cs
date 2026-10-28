using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

[Scoped<ICharacterService>]
public class CharacterService(DungeonWorldContext dungeonWorldContext) : ICharacterService
{
    // One query per table: joining them all at once multiplies every list by every other (tens of thousands of rows)
    private IQueryable<Character> CharactersWithDetails =>
        dungeonWorldContext.Characters
            .AsSplitQuery()
            .Include(c => c.Class)
            .Include(c => c.Health)
            .Include(c => c.Stats)
            .Include(c => c.Bonds)
            .Include(c => c.Debilities)
            .Include(c => c.Abilities)
            .Include(c => c.Resources)
            .Include(c => c.ArmorBonuses)
            .Include(c => c.Transformations)
            .Include(c => c.Goals)
            .Include(c => c.Notes)
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

    public Task<List<CharacterName>> GetCharacterNamesAsync()
    {
        // The full sheets join a dozen tables into tens of thousands of rows, which is too slow for autocomplete
        return dungeonWorldContext.Characters
            .Select(c => new CharacterName(c.Name, c.Class.Type))
            .ToListAsync();
    }

    public Task<string?> GetCharacterNameAsync(Snowflake userId)
    {
        return dungeonWorldContext.Characters
            .Where(c => c.ID == userId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync();
    }

    public Task<List<Character>> GetCharacterAbilitiesAsync()
    {
        return dungeonWorldContext.Characters
            .AsNoTracking()
            .Include(c => c.Abilities)
            .ToListAsync();
    }

    public async Task AddCharacterAsync(Character character)
    {
        dungeonWorldContext.Characters.Add(character);
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task BondWithAsync(Character character, Character bonder)
    {
        character.Bonds ??= [];
        character.Bonds.Add(new Bond { TargetID = bonder.ID, TargetName = bonder.Name });

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task DamageAsync(Character character, int amount)
    {
        character.Health.CurrentHP = Math.Max(0, character.Health.CurrentHP - amount);
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task HealAsync(Character character, int amount)
    {
        character.Health.CurrentHP = Math.Min(character.Health.MaxHP, character.Health.CurrentHP + amount);
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task FullHealAsync(Character character)
    {
        character.Health.CurrentHP = character.Health.MaxHP;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task UpdateCharacterStatAsync(Character character, StatType statType, int value)
    {
        var stat = character.Stats?.FirstOrDefault(s => s.StatType == statType);
        stat?.Value = value;

        // Max HP is derived from constitution
        if (statType == StatType.Con)
        {
            character.Health.MaxHP = character.Class.HealthModifier + value;
            character.Health.CurrentHP = Math.Min(character.Health.CurrentHP, character.Health.MaxHP);
        }

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task UpdateCharacterDamageModifierAsync(Character character, string modifier)
    {
        character.DamageModifier = modifier;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task AdjustArmorAsync(Character character, int amount)
    {
        character.ArmorRating += amount;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task SetLevelAsync(Character character, int level)
    {
        character.Level = level;
        await dungeonWorldContext.SaveChangesAsync();
    }
}

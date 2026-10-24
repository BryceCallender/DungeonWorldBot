using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

[Scoped<ICharacterService>]
public class CharacterService : ICharacterService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public CharacterService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    // One query per table: joining them all at once multiplies every list by every other (tens of thousands of rows)
    private IQueryable<Character> CharactersWithDetails =>
        _dungeonWorldContext.Characters
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
        // A character they're running for someone else wins over their own while it's in play
        Snowflake? controller = userId;
        return CharactersWithDetails
            .Where(c => c.ID == userId || (c.ControllerID == controller && c.InPlay))
            .OrderBy(c => c.ID == userId)
            .FirstOrDefaultAsync();
    }

    public Task<Character?> GetCharacterAsync(Snowflake characterId)
    {
        return CharactersWithDetails.FirstOrDefaultAsync(c => c.ID == characterId);
    }

    public Task<List<CharacterName>> GetControlledCharacterNamesAsync(Snowflake userId)
    {
        Snowflake? controller = userId;
        return _dungeonWorldContext.Characters
            .Where(c => c.ControllerID == controller)
            .Select(c => new CharacterName(c.Name, c.Class.Type))
            .ToListAsync();
    }

    public async Task PlayAsAsync(Snowflake userId, string? name)
    {
        Snowflake? controller = userId;
        var controlled = await _dungeonWorldContext.Characters.Where(c => c.ControllerID == controller).ToListAsync();

        // Only one at a time, and none means they're back to their own
        foreach (var character in controlled)
            character.InPlay = name is not null && character.Name.Equals(name, StringComparison.OrdinalIgnoreCase);

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public Task<List<Character>> GetCharactersAsync()
    {
        return CharactersWithDetails.ToListAsync();
    }

    public Task<List<CharacterName>> GetCharacterNamesAsync()
    {
        // The full sheets join a dozen tables into tens of thousands of rows, which is too slow for autocomplete
        return _dungeonWorldContext.Characters
            .Select(c => new CharacterName(c.Name, c.Class.Type))
            .ToListAsync();
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

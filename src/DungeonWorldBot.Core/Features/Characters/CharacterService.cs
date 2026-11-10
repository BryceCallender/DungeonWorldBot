using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Characters;

[Scoped<ICharacterService>]
public class CharacterService(DungeonWorldContext dungeonWorldContext, ICommandUser? commandUser = null) : ICharacterService
{
    // Everything the rules read, one query per table: joining them all at once multiplies every list by every other
    // (tens of thousands of rows). Goals and notes are left out, since only the dashboard's journal shows them.
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
            .Include(c => c.Techniques)
            .Include(c => c.Inventory)
            .ThenInclude(i => i.Items);

    public async Task<Character?> GetCharacterFromUserAsync(Snowflake userId)
    {
        var id = await PlayedByAsync(userId);
        return await CharactersWithDetails.FirstOrDefaultAsync(c => c.ID == id);
    }

    public async Task<Character?> GetCharacterToShowAsync(Snowflake userId)
    {
        var id = await PlayedByAsync(userId);
        return await CharactersWithDetails.AsNoTracking().FirstOrDefaultAsync(c => c.ID == id);
    }

    public async Task<Character?> GetCharacterWithJournalAsync(Snowflake userId)
    {
        var id = await PlayedByAsync(userId);
        return await CharactersWithDetails.AsNoTracking().Include(c => c.Goals).Include(c => c.Notes).FirstOrDefaultAsync(c => c.ID == id);
    }

    public async Task<Character?> GetCharacterMovesAsync(Snowflake userId)
    {
        var id = await PlayedByAsync(userId);
        return await dungeonWorldContext.Characters
            .AsNoTracking()
            .Include(c => c.Class)
            .Include(c => c.Abilities)
            .FirstOrDefaultAsync(c => c.ID == id);
    }

    /// <summary>
    /// The character the user is playing: one they've taken over, when they're the one running the command, or else
    /// their own. Anyone else asked about is left as they are, so their own character can still be picked out.
    /// </summary>
    private async Task<Snowflake> PlayedByAsync(Snowflake userId)
    {
        if (commandUser?.ID != userId)
            return userId;

        var taken = await dungeonWorldContext.Characters
            .Where(c => c.ControlledByID == userId)
            .Select(c => c.ID)
            .ToListAsync();
        return taken is [var id] ? id : userId;
    }

    public Task<List<Character>> GetCharactersAsync()
    {
        return CharactersWithDetails.ToListAsync();
    }

    public Task<List<CharacterName>> GetCharacterNamesAsync()
    {
        // The full sheets join a dozen tables into tens of thousands of rows, which is too slow for autocomplete
        return dungeonWorldContext.Characters
            .Select(c => new CharacterName(c.ID, c.Name, c.Class.Type, c.ControlledByID))
            .ToListAsync();
    }

    public async Task<string?> GetCharacterNameAsync(Snowflake userId)
    {
        var id = await PlayedByAsync(userId);
        return await dungeonWorldContext.Characters
            .Where(c => c.ID == id)
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

    public async Task TakeOverAsync(Snowflake characterId, Snowflake userId)
    {
        // Let go of whoever they were playing first, since someone plays one character at a time
        await using var transaction = await dungeonWorldContext.Database.BeginTransactionAsync();
        await dungeonWorldContext.Characters
            .Where(c => c.ControlledByID == userId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ControlledByID, (Snowflake?)null));
        await dungeonWorldContext.Characters
            .Where(c => c.ID == characterId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ControlledByID, userId));
        await transaction.CommitAsync();
    }

    public Task ReleaseAsync(Snowflake characterId)
    {
        return dungeonWorldContext.Characters
            .Where(c => c.ID == characterId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.ControlledByID, (Snowflake?)null));
    }

    public async Task AddCharacterAsync(Character character)
    {
        dungeonWorldContext.Characters.Add(character);
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task BondWithAsync(Character character, Character bonder)
    {
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
        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
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

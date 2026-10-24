using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Combat;

[Scoped<IArmorService>]
public class ArmorService : IArmorService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public ArmorService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    public async Task<DamageTaken> TakeDamageAsync(Character character, int damage, DamageType type, int piercing = 0)
    {
        damage = Math.Max(0, damage);
        var armor = DamageRules.ArmorAgainst(character, type);
        var taken = DamageRules.Taken(damage, armor, piercing);
        character.Health.CurrentHP = Math.Max(0, character.Health.CurrentHP - taken);

        // Armor that lasts until hit still counts against the hit that breaks it
        var broken = damage > 0 ? Remove(character, b => b.Duration == ArmorDuration.UntilHit) : [];

        await _dungeonWorldContext.SaveChangesAsync();
        return new DamageTaken(damage, type, armor, piercing, taken, broken);
    }

    public async Task SetArmorAsync(Character character, ArmorType type, int value)
    {
        switch (type)
        {
            case ArmorType.Physical:
                character.ArmorRating = value;
                break;
            case ArmorType.Magical:
                character.MagicArmor = value;
                break;
            default:
                character.DivineArmor = value;
                break;
        }

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task AddBonusAsync(Character character, string source, ArmorType type, int amount, ArmorDuration duration)
    {
        character.ArmorBonuses ??= new List<ArmorBonus>();
        character.ArmorBonuses.Add(new ArmorBonus { Source = source, Type = type, Amount = amount, Duration = duration });
        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ArmorBonus>> RemoveBonusesAsync(Character character, string? source = null)
    {
        var removed = Remove(character, b => source is null || string.Equals(b.Source, source.Trim(), StringComparison.OrdinalIgnoreCase));
        await _dungeonWorldContext.SaveChangesAsync();
        return removed;
    }

    public async Task<IReadOnlyList<ArmorBonus>> EndFightAsync(Character character)
    {
        var removed = Remove(character, b => b.Duration != ArmorDuration.UntilRemoved);
        await _dungeonWorldContext.SaveChangesAsync();
        return removed;
    }

    public async Task<IReadOnlyList<(string Name, IReadOnlyList<ArmorBonus> Removed)>> EndFightForAllAsync() =>
        await EndFightForAsync(await _dungeonWorldContext.Characters.Include(c => c.ArmorBonuses).ToListAsync());

    public async Task<IReadOnlyList<(string Name, IReadOnlyList<ArmorBonus> Removed)>> EndFightForAsync(IReadOnlyCollection<Snowflake> characters)
    {
        var ids = characters.ToHashSet();
        // Few enough to sort out here, which saves teaching SQLite to compare Discord IDs
        var all = await _dungeonWorldContext.Characters.Include(c => c.ArmorBonuses).ToListAsync();
        return await EndFightForAsync(all.Where(c => ids.Contains(c.ID)).ToList());
    }

    private async Task<IReadOnlyList<(string Name, IReadOnlyList<ArmorBonus> Removed)>> EndFightForAsync(List<Character> characters)
    {

        var cleared = new List<(string Name, IReadOnlyList<ArmorBonus> Removed)>();
        foreach (var character in characters)
        {
            var removed = Remove(character, b => b.Duration != ArmorDuration.UntilRemoved);
            if (removed.Count > 0)
                cleared.Add((character.Name, removed));
        }

        await _dungeonWorldContext.SaveChangesAsync();
        return cleared;
    }

    private List<ArmorBonus> Remove(Character character, Func<ArmorBonus, bool> predicate)
    {
        var removed = character.ArmorBonuses?.Where(predicate).ToList() ?? [];
        foreach (var bonus in removed)
        {
            character.ArmorBonuses!.Remove(bonus);
            _dungeonWorldContext.Remove(bonus);
        }

        return removed;
    }
}

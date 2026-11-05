using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Combat;

[Scoped<IArmorService>]
public class ArmorService(DungeonWorldContext dungeonWorldContext) : IArmorService
{
    public async Task<DamageTaken> TakeDamageAsync(Character character, int damage, DamageType type, int piercing = 0)
    {
        damage = Math.Max(0, damage);
        var armor = DamageRules.ArmorAgainst(character, type);
        var taken = DamageRules.Taken(damage, armor, piercing);
        character.Health.CurrentHP = Math.Max(0, character.Health.CurrentHP - taken);

        // Armor that lasts until hit still counts against the hit that breaks it
        var broken = damage > 0 ? Remove(character, b => b.Duration == ArmorDuration.UntilHit) : [];

        await dungeonWorldContext.SaveChangesAsync();
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

        await dungeonWorldContext.SaveChangesAsync();
    }

    public Task AddBonusAsync(Character character, string source, ArmorType type, int amount, ArmorDuration duration) =>
        AddBonusAsync([character], source, type, amount, duration);

    public async Task AddBonusAsync(IEnumerable<Character> characters, string source, ArmorType type, int amount, ArmorDuration duration)
    {
        foreach (var character in characters)
        {
            character.ArmorBonuses ??= [];
            character.ArmorBonuses.Add(new ArmorBonus { Source = source, Type = type, Amount = amount, Duration = duration });
        }

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ArmorBonus>> RemoveBonusesAsync(Character character, string? source = null)
    {
        var removed = Remove(character, FromSource(source));
        await dungeonWorldContext.SaveChangesAsync();
        return removed;
    }

    public async Task RemoveBonusesAsync(IEnumerable<Character> characters, string source)
    {
        foreach (var character in characters)
            Remove(character, FromSource(source));

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ArmorBonus>> EndFightAsync(Character character)
    {
        var removed = Remove(character, b => b.Duration != ArmorDuration.UntilRemoved);
        await dungeonWorldContext.SaveChangesAsync();
        return removed;
    }

    public async Task<IReadOnlyList<ClearedArmor>> EndFightForAllAsync() =>
        await EndFightForAsync(await WithArmor.ToListAsync());

    public async Task<IReadOnlyList<ClearedArmor>> EndFightForAsync(IReadOnlyCollection<Snowflake> characters) =>
        await EndFightForAsync(await WithArmor.Where(c => characters.Contains(c.ID)).ToListAsync());

    // Only their armor: ending a fight touches nothing else on the sheet
    private IQueryable<Character> WithArmor => dungeonWorldContext.Characters.Include(c => c.ArmorBonuses);

    private async Task<IReadOnlyList<ClearedArmor>> EndFightForAsync(List<Character> characters)
    {
        List<ClearedArmor> cleared = [];
        foreach (var character in characters)
        {
            var removed = Remove(character, b => b.Duration != ArmorDuration.UntilRemoved);
            if (removed.Count > 0)
                cleared.Add(new ClearedArmor(character.Name, removed));
        }

        await dungeonWorldContext.SaveChangesAsync();
        return cleared;
    }

    private static Func<ArmorBonus, bool> FromSource(string? source) =>
        b => source is null || string.Equals(b.Source, source.Trim(), StringComparison.OrdinalIgnoreCase);

    private List<ArmorBonus> Remove(Character character, Func<ArmorBonus, bool> predicate)
    {
        var removed = character.ArmorBonuses?.Where(predicate).ToList() ?? [];
        foreach (var bonus in removed)
        {
            character.ArmorBonuses!.Remove(bonus);
            dungeonWorldContext.Remove(bonus);
        }

        return removed;
    }
}

using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

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
        if (type == ArmorType.Physical)
            character.ArmorRating = value;
        else
            character.MagicArmor = value;

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

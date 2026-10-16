using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Debilities;

[Scoped<IDebilityService>]
public class DebilityService : IDebilityService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public DebilityService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    public async Task<bool> AddAsync(Character character, DebilityType debility)
    {
        if (DebilityRules.Has(character, debility))
            return false;

        character.Debilities ??= new List<Debility>();
        character.Debilities.Add(new Debility { StatType = DebilityRules.StatFor(debility), Modifier = DebilityRules.Penalty });

        await _dungeonWorldContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAsync(Character character, DebilityType debility)
    {
        var stat = DebilityRules.StatFor(debility);
        var matching = character.Debilities?.Where(d => d.StatType == stat).ToList() ?? [];
        if (matching.Count == 0)
            return false;

        foreach (var entry in matching)
        {
            character.Debilities!.Remove(entry);
            _dungeonWorldContext.Remove(entry);
        }

        await _dungeonWorldContext.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<DebilityType>> ClearAsync(Character character)
    {
        var removed = DebilityRules.Current(character);
        foreach (var entry in character.Debilities?.ToList() ?? [])
        {
            character.Debilities!.Remove(entry);
            _dungeonWorldContext.Remove(entry);
        }

        await _dungeonWorldContext.SaveChangesAsync();
        return removed;
    }
}

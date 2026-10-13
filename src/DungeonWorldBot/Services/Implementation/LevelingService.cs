using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Spells;

namespace DungeonWorldBot.Services.Implementation;

public class LevelingService : ILevelingService
{
    /// <summary>
    /// Stats can't be raised above 18 (+3).
    /// </summary>
    public const int MaxStat = 18;

    // Stored like a class resource, but every class has it and it isn't shown with the class resources
    private static readonly ClassResource StatPoints = new("Stat Points", "Gained on level up and spent to raise a stat by 1.") { ResetWithAll = false };

    private readonly ICharacterService _characterService;
    private readonly IResourceService _resourceService;

    public LevelingService(ICharacterService characterService, IResourceService resourceService)
    {
        _characterService = characterService;
        _resourceService = resourceService;
    }

    public async Task ChangeLevelAsync(Character character, int level)
    {
        var change = level - character.Level;
        await _characterService.SetLevelAsync(character, level);

        if (change > 0)
            await GrantStatPointsAsync(character, change);

        foreach (var resource in LevelBasedResources(character))
        {
            var value = _resourceService.GetValue(character, resource);
            await _resourceService.SetValueAsync(character, resource, Math.Max(1, value + change));
        }
    }

    public int GetStatPoints(Character character) => _resourceService.GetValue(character, StatPoints);

    public async Task GrantStatPointsAsync(Character character, int amount)
    {
        await _resourceService.SetValueAsync(character, StatPoints, GetStatPoints(character) + amount);
    }

    public async Task<string?> AllocateStatAsync(Character character, StatType statType)
    {
        var points = GetStatPoints(character);
        if (points <= 0)
            return $"{character.Name} has no stat points to spend. You get one each time you level up.";

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return $"{character.Name} has no {statType} stat.";

        if (stat.Value >= MaxStat)
            return $"{statType} is already {stat.Value}. Stats can't go above {MaxStat}.";

        await _characterService.UpdateCharacterStatAsync(character, statType, stat.Value + 1);
        await _resourceService.SetValueAsync(character, StatPoints, points - 1);
        return null;
    }

    /// <summary>
    /// Resources that follow the character's level: their own sheet's, and caster levels from moves learned from other classes.
    /// </summary>
    private static IEnumerable<ClassResource> LevelBasedResources(Character character)
    {
        var own = ClassSheetLibrary.For(character.Class.Type)?.ResourcesFor(ClassSheetLibrary.KnownMoves(character)) ?? [];
        var casting = SpellCasting.Access(character).Select(a => a.LevelResource);

        return own.Concat(casting)
            .Where(r => r.GrowsWithLevel)
            .DistinctBy(r => r.Name, StringComparer.OrdinalIgnoreCase);
    }
}

using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Leveling;

[Scoped<ILevelingService>]
public class LevelingService(
    ICharacterService characterService,
    IResourceService resourceService,
    IGameSettingsService? settings = null,
    ISubclassService? subclasses = null)
    : ILevelingService
{
    /// <summary>
    /// Stats can't be raised above 18 (+3).
    /// </summary>
    public const int MaxStat = 18;

    // Stored like a class resource, but every class has it and it isn't shown with the class resources
    private static readonly ClassResource StatPoints = new("Stat Points", "Gained on level up and spent to raise a stat by 1.") { ResetWithAll = false };

    public async Task ChangeLevelAsync(Character character, int level)
    {
        var previous = character.Level;
        var change = level - previous;
        await characterService.SetLevelAsync(character, level);

        // Each level reached gives the DM's chosen amount of stat points
        var points = Enumerable.Range(previous + 1, Math.Max(0, change)).Sum(l => settings?.StatPointsForLevel(l) ?? 1);
        if (points > 0)
            await GrantStatPointsAsync(character, points);

        // Most of these are worked out from the level already. The rest are from moves taken before levels were recorded,
        // and setting them records when the move was taken.
        foreach (var resource in LevelBasedResources(character).Where(r => !resourceService.TracksLevel(character, r)))
        {
            var value = resourceService.GetValue(character, resource);
            await resourceService.SetValueAsync(character, resource, Math.Max(1, value + change));
        }

        // Subclass moves unlock by level. Leveling down keeps them, like advanced moves.
        if (subclasses is not null)
            await subclasses.GrantUnlockedMovesAsync(character);
    }

    public int GetStatPoints(Character character) => resourceService.GetValue(character, StatPoints);

    public async Task GrantStatPointsAsync(Character character, int amount)
    {
        await resourceService.SetValueAsync(character, StatPoints, GetStatPoints(character) + amount);
    }

    public async Task<string?> AllocateStatAsync(Character character, StatType statType)
    {
        var points = GetStatPoints(character);
        if (points <= 0)
            return $"{character.Name} has no stat points to spend. The DM hands them out when the party levels up.";

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return $"{character.Name} has no {statType} stat.";

        if (stat.Value >= MaxStat)
            return $"{statType} is already {stat.Value}. Stats can't go above {MaxStat}.";

        await characterService.UpdateCharacterStatAsync(character, statType, stat.Value + 1);
        await resourceService.SetValueAsync(character, StatPoints, points - 1);
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

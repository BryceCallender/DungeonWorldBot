using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Techniques;

[Scoped<ITechniqueService>]
public class TechniqueService(
    DungeonWorldContext dungeonWorldContext,
    IArmorService armorService)
    : ITechniqueService
{
    public async Task<Technique> DevelopAsync(Character character, string name, TechniqueTag tags, bool signature)
    {
        var technique = new Technique
        {
            Name = name.Trim(),
            Tags = tags,
            IsSignature = signature,
        };

        character.Techniques.Add(technique);

        await dungeonWorldContext.SaveChangesAsync();
        return technique;
    }

    public async Task UpdateAsync(Technique technique, string? name, TechniqueTag? tags)
    {
        if (!string.IsNullOrWhiteSpace(name))
            technique.Name = name.Trim();
        if (tags is { } newTags)
            technique.Tags = newTags;

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task ForgetAsync(Character character, Technique technique)
    {
        character.Techniques.Remove(technique);
        dungeonWorldContext.Remove(technique);
        await dungeonWorldContext.SaveChangesAsync();
    }

    public Task<IReadOnlyList<ArmorBonus>> DropBarrierAsync(Character character) =>
        armorService.RemoveBonusesAsync(character, TechniqueRules.BarrierSource);

    public async Task<int> RaiseBarrierAsync(Character character)
    {
        var armor = TechniqueRules.BarrierArmor(character);
        await armorService.AddBonusAsync(character, TechniqueRules.BarrierSource, ArmorType.Physical, armor, ArmorDuration.UntilRemoved);
        return armor;
    }
}

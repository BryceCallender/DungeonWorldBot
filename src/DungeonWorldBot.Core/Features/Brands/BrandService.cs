using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Brands;

public interface IBrandService
{
    /// <summary>
    /// Conjures a new brand with these tags and 3 uses, letting any old one gutter out.
    /// </summary>
    Task<Weapon> ConjureAsync(Character character, IReadOnlyList<string> tags);

    /// <summary>
    /// Uses up one of the brand's uses for an attack. At none left, it gutters out.
    /// </summary>
    /// <returns>The uses left.</returns>
    Task<int> SpendAsync(Character character);
}

[Scoped<IBrandService>]
public class BrandService(IResourceService resourceService) : IBrandService
{
    public async Task<Weapon> ConjureAsync(Character character, IReadOnlyList<string> tags)
    {
        RemoveBrand(character);

        var brand = BrandRules.Forge(tags);
        character.Inventory.Items ??= [];
        character.Inventory.Items.Add(brand);

        // Saves the brand too
        await resourceService.SetValueAsync(character, BrandRules.UsesResource, BrandRules.Uses);
        return brand;
    }

    public async Task<int> SpendAsync(Character character)
    {
        var left = resourceService.GetValue(character, BrandRules.UsesResource) - 1;
        if (left <= 0)
            RemoveBrand(character);

        return await resourceService.SetValueAsync(character, BrandRules.UsesResource, left);
    }

    private static void RemoveBrand(Character character)
    {
        if (BrandRules.Brand(character) is { } old)
            character.Inventory.Items!.Remove(old);
    }
}

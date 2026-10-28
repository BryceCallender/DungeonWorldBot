using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Resources;

[Scoped<IResourceService>]
public class ResourceService(DungeonWorldContext dungeonWorldContext) : IResourceService
{
    public int GetValue(Character character, ClassResource resource)
    {
        return LevelBasedValue(character, resource)
               ?? Find(character, resource)?.Value
               ?? resource.StartFor(ClassSheetLibrary.KnownMoves(character));
    }

    public bool TracksLevel(Character character, ClassResource resource) => LevelBasedValue(character, resource) is not null;

    public int? GetMax(Character character, ClassResource resource)
    {
        return resource.MaxFor(ClassSheetLibrary.KnownMoves(character));
    }

    public int GetResetValue(Character character, ClassResource resource)
    {
        return resource.ResetValueFor(ClassSheetLibrary.KnownMoves(character));
    }

    public string Format(Character character, ClassResource resource)
    {
        var value = GetValue(character, resource);
        return GetMax(character, resource) is { } max ? $"{value}/{max}" : value.ToString();
    }

    public async Task<int> SetValueAsync(Character character, ClassResource resource, int value)
    {
        if (resource.GrowsWithLevel)
        {
            var (followsLevel, ability) = LevelSource(character, resource);
            if (followsLevel)
                return character.Level;

            if (ability is not null)
            {
                // Setting the level moves when the ability was taken, which also fills it in for abilities from before it was recorded
                value = Math.Clamp(value, 1, character.Level);
                ability.LevelTaken = character.Level - value + 1;
                await dungeonWorldContext.SaveChangesAsync();
                return value;
            }
        }

        value = Math.Max(0, value);
        if (GetMax(character, resource) is { } max)
            value = Math.Min(max, value);

        var stored = Find(character, resource);
        if (stored is null)
        {
            stored = new CharacterResource { Name = resource.Name };
            character.Resources ??= [];
            character.Resources.Add(stored);
        }

        stored.Value = value;
        await dungeonWorldContext.SaveChangesAsync();
        return value;
    }

    /// <summary>
    /// A resource that grows with level, like a caster level: the character's level for their own class's,
    /// or 1 at the level they took the move that grants it (like Divine Favor) and +1 each level after.
    /// Null when it doesn't grow with level, or the move was taken before levels were recorded.
    /// </summary>
    private static int? LevelBasedValue(Character character, ClassResource resource)
    {
        if (!resource.GrowsWithLevel)
            return null;

        var (followsLevel, ability) = LevelSource(character, resource);
        if (followsLevel)
            return character.Level;

        return ability?.LevelTaken is { } taken ? Math.Max(1, character.Level - taken + 1) : null;
    }

    /// <summary>
    /// Whether the resource follows the character's own level, and otherwise the ability it counts up from.
    /// </summary>
    private static (bool FollowsLevel, Ability? Ability) LevelSource(Character character, ClassResource resource)
    {
        var ownSheet = ClassSheetLibrary.For(character.Class.Type);
        var grantedBy = resource.GrantedBy;

        // A caster level from another class's sheet counts from the move that was learned from it
        if (grantedBy is null && !ReferenceEquals(ownSheet?.FindResource(resource.Name), resource))
            grantedBy = SpellCasting.Access(character).FirstOrDefault(a => ReferenceEquals(a.LevelResource, resource))?.GrantedBy;

        if (grantedBy is null)
            return (true, null);

        return (false, character.Abilities?.FirstOrDefault(a => a.Name.Equals(grantedBy, StringComparison.OrdinalIgnoreCase)));
    }

    private static CharacterResource? Find(Character character, ClassResource resource) =>
        character.Resources?.FirstOrDefault(r => r.Name.Equals(resource.Name, StringComparison.OrdinalIgnoreCase));
}

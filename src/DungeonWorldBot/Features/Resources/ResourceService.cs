using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Resources;

[Scoped<IResourceService>]
public class ResourceService : IResourceService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public ResourceService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    public int GetValue(Character character, ClassResource resource)
    {
        return Find(character, resource)?.Value ?? resource.StartFor(ClassSheetLibrary.KnownMoves(character));
    }

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
        value = Math.Max(0, value);
        if (GetMax(character, resource) is { } max)
            value = Math.Min(max, value);

        var stored = Find(character, resource);
        if (stored is null)
        {
            stored = new CharacterResource { Name = resource.Name };
            character.Resources ??= new List<CharacterResource>();
            character.Resources.Add(stored);
        }

        stored.Value = value;
        await _dungeonWorldContext.SaveChangesAsync();
        return value;
    }

    private static CharacterResource? Find(Character character, ClassResource resource) =>
        character.Resources?.FirstOrDefault(r => r.Name.Equals(resource.Name, StringComparison.OrdinalIgnoreCase));
}

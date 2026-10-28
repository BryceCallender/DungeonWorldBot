using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Shapeshifting;

[Scoped<ITransformationService>]
public class TransformationService(
    DungeonWorldContext dungeonWorldContext,
    IResourceService resourceService)
    : ITransformationService
{
    public async Task<Transformation> LearnAsync(Character character, string name, string? description, string? moves)
    {
        var form = new Transformation
        {
            Name = name.Trim(),
            Description = Clean(description),
            Moves = Clean(moves),
        };

        character.Transformations ??= [];
        character.Transformations.Add(form);

        await dungeonWorldContext.SaveChangesAsync();
        return form;
    }

    public async Task UpdateAsync(Transformation form, string? description, string? moves)
    {
        if (description is not null)
            form.Description = Clean(description);
        if (moves is not null)
            form.Moves = Clean(moves);

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task ForgetAsync(Character character, Transformation form)
    {
        character.Transformations?.Remove(form);
        dungeonWorldContext.Remove(form);
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task ShiftAsync(Character character, IReadOnlyList<Transformation> forms, int hold)
    {
        foreach (var form in character.Transformations ?? [])
            form.IsActive = forms.Contains(form);

        foreach (var form in forms)
            form.TimesTaken++;

        // Saves the forms too
        await resourceService.SetValueAsync(character, ShapeshiftRules.Hold, hold);
    }

    public async Task<IReadOnlyList<Transformation>> RevertAsync(Character character)
    {
        var active = ShapeshiftRules.Active(character);
        foreach (var form in active)
            form.IsActive = false;

        await resourceService.SetValueAsync(character, ShapeshiftRules.Hold, 0);
        return active;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

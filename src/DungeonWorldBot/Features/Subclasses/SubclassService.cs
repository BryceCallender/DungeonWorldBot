using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Subclasses;

[Scoped<ISubclassService>]
public class SubclassService(IAbilityService abilityService) : ISubclassService
{
    public async Task<IReadOnlyList<ClassMove>> JoinAsync(Character character, Subclass subclass)
    {
        if (abilityService.FindAbility(character, subclass.Name) is null)
            await abilityService.AddAbilityAsync(character, subclass.Name, $"*{subclass.Title}.* {subclass.Summary}", null);

        return await GrantUnlockedMovesAsync(character);
    }

    public async Task LeaveAsync(Character character, Subclass subclass)
    {
        var names = subclass.Moves.Select(m => m.Move.Name).Concat(subclass.StateAbilities).Append(subclass.Name);
        foreach (var name in names)
        {
            if (abilityService.FindAbility(character, name) is { } ability)
                await abilityService.RemoveAbilityAsync(character, ability);
        }
    }

    public async Task<IReadOnlyList<ClassMove>> GrantUnlockedMovesAsync(Character character)
    {
        if (SubclassLibrary.Of(character) is not { } subclass)
            return [];

        List<ClassMove> learned = [];
        foreach (var move in subclass.MovesAt(character.Level))
        {
            if (abilityService.FindAbility(character, move.Name) is not null)
                continue;

            await abilityService.AddAbilityAsync(character, move.Name, move.Summary, move.Uses);
            learned.Add(move);
        }

        return learned;
    }
}

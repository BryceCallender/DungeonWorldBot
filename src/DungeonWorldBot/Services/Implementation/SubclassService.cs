using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Subclasses;

namespace DungeonWorldBot.Services.Implementation;

public class SubclassService : ISubclassService
{
    private readonly IAbilityService _abilityService;

    public SubclassService(IAbilityService abilityService)
    {
        _abilityService = abilityService;
    }

    public async Task<IReadOnlyList<ClassMove>> JoinAsync(Character character, Subclass subclass)
    {
        if (_abilityService.FindAbility(character, subclass.Name) is null)
            await _abilityService.AddAbilityAsync(character, subclass.Name, $"*{subclass.Title}.* {subclass.Summary}", null);

        return await GrantUnlockedMovesAsync(character);
    }

    public async Task LeaveAsync(Character character, Subclass subclass)
    {
        var names = subclass.Moves.Select(m => m.Move.Name).Concat(subclass.StateAbilities).Append(subclass.Name);
        foreach (var name in names)
        {
            if (_abilityService.FindAbility(character, name) is { } ability)
                await _abilityService.RemoveAbilityAsync(character, ability);
        }
    }

    public async Task<IReadOnlyList<ClassMove>> GrantUnlockedMovesAsync(Character character)
    {
        if (SubclassLibrary.Of(character) is not { } subclass)
            return [];

        var learned = new List<ClassMove>();
        foreach (var move in subclass.MovesAt(character.Level))
        {
            if (_abilityService.FindAbility(character, move.Name) is not null)
                continue;

            await _abilityService.AddAbilityAsync(character, move.Name, move.Summary, move.Uses);
            learned.Add(move);
        }

        return learned;
    }
}

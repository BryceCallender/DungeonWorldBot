using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Debilities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Moves;

[Scoped<ISpecialMoveService>]
public class SpecialMoveService(DungeonWorldContext dungeonWorldContext, IDebilityService debilityService) : ISpecialMoveService
{
    public async Task<CampResult> MakeCampAsync(Character character, bool slept)
    {
        var rations = SpecialMoveRules.Rations(character);
        if (rations is not null)
        {
            rations.Amount--;
            if (rations.Amount == 0)
                character.Inventory.Items!.Remove(rations);
        }

        var healed = slept ? Heal(character, SpecialMoveRules.HealOnWaking(character)) : 0;

        await dungeonWorldContext.SaveChangesAsync();
        return new CampResult(rations?.Name, rations?.Amount ?? 0, healed);
    }

    public async Task<RecoveryResult> RecoverAsync(Character character, int days, bool healer, DebilityType? chosen)
    {
        var healed = days >= 1 ? Heal(character, character.Health.MaxHP) : 0;
        await dungeonWorldContext.SaveChangesAsync();

        var current = DebilityRules.Current(character);
        var earned = Math.Min(SpecialMoveRules.DebilitiesRecovered(days, healer), current.Count);
        IReadOnlyList<DebilityType> removing = earned == current.Count ? current
            : earned > 0 && chosen is { } pick && current.Contains(pick) ? [pick]
            : [];

        foreach (var debility in removing)
            await debilityService.RemoveAsync(character, debility);

        return new RecoveryResult(healed, removing, earned - removing.Count);
    }

    public async Task<bool> SpendCoinsAsync(Character character, int amount)
    {
        if (SpecialMoveRules.Coins(character) is not { } coins || coins.Amount < amount)
            return false;

        coins.Amount -= amount;
        if (coins.Amount == 0)
            character.Inventory.Items!.Remove(coins);

        await dungeonWorldContext.SaveChangesAsync();
        return true;
    }

    private static int Heal(Character character, int amount)
    {
        var before = character.Health.CurrentHP;
        character.Health.CurrentHP = Math.Min(character.Health.MaxHP, before + amount);
        return character.Health.CurrentHP - before;
    }
}

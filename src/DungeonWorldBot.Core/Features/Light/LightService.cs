using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.InverseWorld;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Light;

public interface ILightService
{
    /// <summary>
    /// Forms the Shield of Light around whoever holds it, taking it from wherever it was before.
    /// </summary>
    /// <param name="holder">The Lantern, or an ally they lend it to.</param>
    /// <returns>Everyone it now protects. With Gift from God, a lent shield still protects the Lantern too.</returns>
    Task<IReadOnlyList<Character>> RaiseShieldAsync(Character lantern, Character holder);

    /// <summary>
    /// Lets the Shield of Light fade from everyone it protects.
    /// </summary>
    /// <returns>Whether there was a shield to lower.</returns>
    Task<bool> LowerShieldAsync(Character lantern);
}

/// <summary>
/// The Lantern's Shield of Light, kept as armor on whoever it protects. It's named after the Lantern, so each one's
/// shield can be found and moved on its own.
/// </summary>
[Scoped<ILightService>]
public class LightService(DungeonWorldContext dungeonWorldContext, IArmorService armorService) : ILightService
{
    public const int ShieldArmor = 1;

    public static string ShieldSource(Character lantern) => $"{Lantern.ShieldOfLight} ({lantern.Name})";

    public async Task<IReadOnlyList<Character>> RaiseShieldAsync(Character lantern, Character holder)
    {
        await LowerShieldAsync(lantern);

        List<Character> protectedOnes = [holder];
        if (holder.ID != lantern.ID && ClassSheetLibrary.Knows(lantern, Lantern.GiftFromGod))
            protectedOnes.Add(lantern);

        await armorService.AddBonusAsync(protectedOnes, ShieldSource(lantern), ArmorType.Physical, ShieldArmor, ArmorDuration.UntilRemoved);

        return protectedOnes;
    }

    public async Task<bool> LowerShieldAsync(Character lantern)
    {
        var source = ShieldSource(lantern);
        // Only whoever holds it, with just their armor, rather than every character's whole sheet
        var holders = await dungeonWorldContext.Characters
            .Include(c => c.ArmorBonuses)
            .Where(c => c.ArmorBonuses.Any(b => b.Source == source))
            .ToListAsync();

        await armorService.RemoveBonusesAsync(holders, source);

        return holders.Count > 0;
    }
}

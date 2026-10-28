using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Npcs;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Npcs;

/// <summary>
/// The DM looking after the NPCs travelling with the party, as /dm npc does in Discord. The bot's own rules do the work.
/// </summary>
/// <param name="options">Options for opening the bot's database read-write.</param>
public class NpcDesk(DbContextOptions<DungeonWorldContext> options)
{
    public const int MaxNameLength = FormText.MaxNameLength;
    public const int MaxTextLength = FormText.MaxTextLength;

    // NPCs are written with modifiers, as /dm npc stats takes them
    public const int MaxModifier = 3;

    /// <summary>
    /// Adds an NPC to the party, or changes one already with it.
    /// </summary>
    /// <param name="npcID">The NPC to change, or null for a new one.</param>
    /// <returns>Why it couldn't, or null once it has.</returns>
    public async Task<string?> SaveAsync(int? npcID, Npc details)
    {
        if (FormText.Name(details.Name) is not { } name)
            return $"Give them a name, up to {MaxNameLength} letters.";
        if (details.MaxHP < 1)
            return "Give them at least 1 HP.";
        if (details.Armor < 0)
            return "Armor can't be negative.";
        foreach (var stat in Enum.GetValues<StatType>())
        {
            if (Math.Abs(details.Modifier(stat)) > MaxModifier)
                return $"{stat.ToString().ToUpper()} goes from -{MaxModifier} to +{MaxModifier}.";
        }

        await using var context = new DungeonWorldContext(options);
        var service = new NpcService(context);

        Npc npc;
        if (npcID is null)
            npc = new Npc();
        else if (await context.Npcs.FirstOrDefaultAsync(n => n.ID == npcID) is { } found)
            npc = found;
        else
            return NoNpc;

        if (await service.FindAsync(name) is { } taken && taken.ID != npc.ID)
            return $"{taken.Name} is already with the party.";

        npc.Name = name;
        npc.Role = FormText.Clean(details.Role, MaxNameLength);
        npc.MaxHP = details.MaxHP;
        // A new NPC starts unhurt
        npc.CurrentHP = npcID is null ? details.MaxHP : details.CurrentHP;
        npc.Armor = details.Armor;
        npc.Damage = FormText.Clean(details.Damage, MaxNameLength);
        npc.Loyalty = details.Loyalty;
        npc.Skills = FormText.Clean(details.Skills);
        npc.Cost = FormText.Clean(details.Cost, MaxNameLength);
        npc.Notes = FormText.Clean(details.Notes);
        npc.FollowsID = details.FollowsID;
        foreach (var type in Enum.GetValues<StatType>())
            npc.SetModifier(type, details.Modifier(type));

        if (npcID is null)
        {
            await service.AddAsync(npc);
        }
        else
        {
            // Keeps current HP between 0 and the max
            await service.SaveAsync(npc);
        }

        return null;
    }

    /// <summary>
    /// Hurts or heals an NPC. Damage is reduced by their armor.
    /// </summary>
    public async Task<string?> ChangeHpAsync(int npcID, HpChange change, int amount)
    {
        if (amount < 1)
            return "Give an amount of at least 1.";

        await using var context = new DungeonWorldContext(options);
        var service = new NpcService(context);
        if (await context.Npcs.FirstOrDefaultAsync(n => n.ID == npcID) is not { } npc)
            return NoNpc;

        if (change == HpChange.Damage)
            await service.DamageAsync(npc, amount);
        else
            await service.HealAsync(npc, amount);
        return null;
    }

    /// <summary>
    /// Sends an NPC away from the party. They drop out of any fight they're in, and what they did in fights stays.
    /// </summary>
    public async Task<string?> RemoveAsync(int npcID)
    {
        await using var context = new DungeonWorldContext(options);
        if (await context.Npcs.FirstOrDefaultAsync(n => n.ID == npcID) is not { } npc)
            return NoNpc;

        await new NpcService(context).RemoveAsync(npc);
        return null;
    }

    private const string NoNpc = "That NPC isn't with the party any more.";
}

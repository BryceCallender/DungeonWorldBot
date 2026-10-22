using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Npcs;

public interface INpcService
{
    Task<List<Npc>> GetAllAsync();

    /// <summary>
    /// An NPC by name, whatever its capitalization.
    /// </summary>
    Task<Npc?> FindAsync(string name);

    Task AddAsync(Npc npc);

    /// <summary>
    /// Saves changes made to an NPC's fields.
    /// </summary>
    Task SaveAsync(Npc npc);

    Task RemoveAsync(Npc npc);

    /// <summary>
    /// Damages an NPC. Their armor reduces physical damage, less whatever the hit pierces; other types go straight through.
    /// </summary>
    Task<ArmoredHit> DamageAsync(Npc npc, int amount, DamageType type = DamageType.Physical, int piercing = 0);

    /// <summary>
    /// Heals an NPC, up to their max HP. Leave the amount out to heal them fully.
    /// </summary>
    Task HealAsync(Npc npc, int? amount = null);
}

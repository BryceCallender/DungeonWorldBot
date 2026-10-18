using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Npcs;

[Scoped<INpcService>]
public class NpcService : INpcService
{
    private readonly DungeonWorldContext _context;

    public NpcService(DungeonWorldContext context)
    {
        _context = context;
    }

    public Task<List<Npc>> GetAllAsync() => _context.Npcs.OrderBy(n => n.Name).ToListAsync();

    public Task<Npc?> FindAsync(string name)
    {
        var trimmed = name.Trim();
        // The column compares without case, so this matches "bram" to "Bram"
        return _context.Npcs.FirstOrDefaultAsync(n => n.Name == trimmed);
    }

    public async Task AddAsync(Npc npc)
    {
        _context.Npcs.Add(npc);
        await _context.SaveChangesAsync();
    }

    public async Task SaveAsync(Npc npc)
    {
        npc.CurrentHP = Math.Clamp(npc.CurrentHP, 0, npc.MaxHP);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(Npc npc)
    {
        _context.Npcs.Remove(npc);
        await _context.SaveChangesAsync();
    }

    public async Task<NpcHit> DamageAsync(Npc npc, int amount, DamageType type = DamageType.Physical, int piercing = 0)
    {
        // Modifiers can push a roll below zero ("1d4-3"), which shouldn't heal anyone
        var rolled = Math.Max(0, amount);
        var armor = type == DamageType.Physical ? Math.Max(0, npc.Armor - piercing) : 0;
        var blocked = Math.Min(rolled, armor);
        var taken = rolled - blocked;

        npc.CurrentHP = Math.Max(0, npc.CurrentHP - taken);
        await _context.SaveChangesAsync();

        return new NpcHit(rolled, blocked, taken);
    }

    public async Task HealAsync(Npc npc, int? amount = null)
    {
        npc.CurrentHP = amount is { } heal
            ? Math.Min(npc.MaxHP, npc.CurrentHP + Math.Max(0, heal))
            : npc.MaxHP;
        await _context.SaveChangesAsync();
    }
}

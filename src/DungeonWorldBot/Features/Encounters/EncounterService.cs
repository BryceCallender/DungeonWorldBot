using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

[Scoped<IEncounterService>]
public class EncounterService : IEncounterService
{
    private readonly DungeonWorldContext _context;

    public EncounterService(DungeonWorldContext context)
    {
        _context = context;
    }

    public Task<List<Encounter>> GetRunningAsync() =>
        _context.Encounters
            .Include(e => e.Enemies)
            .Include(e => e.Participants)
            .Where(e => e.EndedAt == null)
            .OrderBy(e => e.ID)
            .ToListAsync();

    public Task<Encounter?> GetOnSceneAsync(string sceneID) =>
        _context.Encounters
            .Include(e => e.Enemies)
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.EndedAt == null && e.SceneID == sceneID);

    public async Task<Encounter?> FindRunningAsync(string name)
    {
        var trimmed = name.Trim();
        // Only a few go at once, and the name column compares with case
        return (await GetRunningAsync()).FirstOrDefault(e => e.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Encounter?> GetForAsync(Snowflake? characterID = null, int? npcID = null) =>
        FightFor(await GetRunningAsync(), characterID, npcID);

    /// <summary>
    /// <see cref="GetForAsync"/>, among fights already looked up.
    /// </summary>
    /// <param name="running">The fights going on, oldest first.</param>
    public static Encounter? FightFor(IReadOnlyList<Encounter> running, Snowflake? characterID = null, int? npcID = null) =>
        running.LastOrDefault(e => e.Participants.Any(p =>
            (characterID is not null && p.CharacterID == characterID) || (npcID is not null && p.NpcID == npcID)))
        ?? running.LastOrDefault(e => e.Participants.Count == 0)
        ?? (running.Count == 1 ? running[0] : null);

    public async Task<EncounterRoster> GetTakenAsync()
    {
        var picked = await _context.EncounterParticipants.Where(p => p.Encounter.EndedAt == null).ToListAsync();
        return new EncounterRoster(
            picked.Where(p => p.CharacterID is not null).Select(p => p.CharacterID!.Value).ToList(),
            picked.Where(p => p.NpcID is not null).Select(p => p.NpcID!.Value).ToList());
    }

    public async Task<Encounter?> StartAsync(string name, SceneLink? scene = null, bool announced = false, EncounterRoster? roster = null)
    {
        if (await FindRunningAsync(name) is not null)
            return null;
        if (scene is not null && await GetOnSceneAsync(scene.ID) is not null)
            return null;

        var taken = await GetTakenAsync();
        var now = DateTimeOffset.UtcNow;
        var encounter = new Encounter
        {
            Name = name.Trim(),
            StartedAt = now,
            SceneID = scene?.ID,
            SceneName = scene?.Name,
            SceneUrl = scene?.Url,
            AnnouncedAt = announced ? now : null,
            Participants = roster is null
                ? []
                : roster.Characters.Except(taken.Characters).Select(id => new EncounterParticipant { CharacterID = id })
                    .Concat(roster.Npcs.Except(taken.Npcs).Select(id => new EncounterParticipant { NpcID = id }))
                    .ToList(),
        };
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync();
        return encounter;
    }

    public async Task EndAsync(Encounter encounter, bool announced = false)
    {
        encounter.EndedAt = DateTimeOffset.UtcNow;
        if (announced)
        {
            // A start nobody announced yet would only be news of a fight that's already over
            encounter.AnnouncedAt ??= encounter.EndedAt;
            encounter.EndAnnouncedAt = encounter.EndedAt;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Snowflake>> LeavingAsync(Encounter ended)
    {
        var picked = (await _context.EncounterParticipants
                .Where(p => p.EncounterID == ended.ID && p.CharacterID != null)
                .Select(p => p.CharacterID)
                .ToListAsync())
            .Select(id => id!.Value)
            .ToList();
        if (picked.Count > 0)
            return picked;

        var elsewhere = (await _context.EncounterParticipants
                .Where(p => p.EncounterID != ended.ID && p.Encounter.EndedAt == null && p.CharacterID != null)
                .Select(p => p.CharacterID)
                .ToListAsync())
            .Select(id => id!.Value)
            .ToHashSet();
        return (await _context.Characters.Select(c => c.ID).ToListAsync()).Where(id => !elsewhere.Contains(id)).ToList();
    }

    public async Task<List<Encounter>> GetUnannouncedAsync()
    {
        var waiting = await _context.Encounters
            .Include(e => e.Enemies)
            .Where(e => e.AnnouncedAt == null || (e.EndedAt != null && e.EndAnnouncedAt == null))
            .ToListAsync();

        // SQLite can't sort by DateTimeOffset, and there are only ever a few of these
        return waiting.OrderBy(e => e.ID).ToList();
    }

    public async Task SaveAsync(Encounter encounter) => await _context.SaveChangesAsync();

    public Task<Enemy?> FindEnemyAsync(string name)
    {
        var trimmed = name.Trim();
        // The column compares without case, so this matches "goblin 2" to "Goblin 2"
        return _context.Enemies.FirstOrDefaultAsync(e => e.Encounter.EndedAt == null && e.Name == trimmed);
    }

    public async Task AddEnemyAsync(Encounter encounter, Enemy enemy)
    {
        encounter.Enemies.Add(enemy);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Enemy>> SpawnAsync(Encounter encounter, MonsterTemplate monster, int count = 1)
    {
        // Numbered across every fight going on, so a name only ever means one enemy
        var taken = await _context.Enemies.Where(e => e.Encounter.EndedAt == null).Select(e => e.Name).ToListAsync();
        var names = SpawnNames(taken.Concat(encounter.Enemies.Select(e => e.Name)), monster.Name, count);
        var spawned = names.Select(name => new Enemy
        {
            Name = name,
            CurrentHP = monster.MaxHP,
            MaxHP = monster.MaxHP,
            Armor = monster.Armor,
            Damage = monster.Damage,
            Tags = monster.Tags,
            Instinct = monster.Instinct,
            Moves = monster.Moves,
            Notes = monster.Notes,
        }).ToList();

        encounter.Enemies.AddRange(spawned);
        await _context.SaveChangesAsync();
        return spawned;
    }

    /// <summary>
    /// Names for new copies of a monster: the plain name for a single copy when there isn't one already, otherwise
    /// numbered after the highest already in the fight. A plain "Goblin" already there counts as Goblin 1.
    /// </summary>
    public static IReadOnlyList<string> SpawnNames(IEnumerable<string> existing, string name, int count)
    {
        var highest = 0;
        foreach (var taken in existing)
        {
            if (taken.Equals(name, StringComparison.OrdinalIgnoreCase))
                highest = Math.Max(highest, 1);
            else if (taken.Length > name.Length + 1
                     && taken.StartsWith(name + " ", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(taken[(name.Length + 1)..], out var number))
                highest = Math.Max(highest, number);
        }

        if (count == 1 && highest == 0)
            return [name];

        return Enumerable.Range(highest + 1, count).Select(n => $"{name} {n}").ToList();
    }

    public async Task SaveAsync(Enemy enemy)
    {
        enemy.CurrentHP = Math.Clamp(enemy.CurrentHP, 0, enemy.MaxHP);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(Enemy enemy)
    {
        _context.Enemies.Remove(enemy);
        await _context.SaveChangesAsync();
    }

    public async Task<ArmoredHit> DamageAsync(Enemy enemy, int amount, DamageType type = DamageType.Physical, int piercing = 0)
    {
        var hit = DamageRules.Hit(amount, enemy.Armor, type, piercing);

        enemy.CurrentHP = Math.Max(0, enemy.CurrentHP - hit.Taken);
        await _context.SaveChangesAsync();

        return hit;
    }

    public async Task HealAsync(Enemy enemy, int? amount = null)
    {
        enemy.CurrentHP = amount is { } heal
            ? Math.Min(enemy.MaxHP, enemy.CurrentHP + Math.Max(0, heal))
            : enemy.MaxHP;
        await _context.SaveChangesAsync();
    }

    public async Task LogAsync(EncounterAction action)
    {
        if (await FightOfAsync(action) is not { } id)
            return;

        action.EncounterID = id;
        if (action.At == default)
            action.At = DateTimeOffset.UtcNow;
        _context.EncounterActions.Add(action);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// The fight something was done in, by its ID: see <see cref="IEncounterService.LogAsync"/>.
    /// </summary>
    private async Task<int?> FightOfAsync(EncounterAction action)
    {
        if (action.EnemyID is { } enemyID)
            return await _context.Enemies.Where(e => e.ID == enemyID && e.Encounter.EndedAt == null).Select(e => (int?)e.EncounterID).FirstOrDefaultAsync();
        if (action.Target is { } target && await FindEnemyAsync(target) is { } aimedAt)
            return aimedAt.EncounterID;
        return (await GetForAsync(action.CharacterID, action.NpcID))?.ID;
    }

    // SQLite can't sort by DateTimeOffset, but IDs go up in the order things happened
    public Task<List<EncounterAction>> GetActionsAsync(Encounter encounter) =>
        _context.EncounterActions.AsNoTracking().Where(a => a.EncounterID == encounter.ID).OrderBy(a => a.ID).ToListAsync();

    public Task<List<MonsterTemplate>> GetMonstersAsync() => _context.MonsterTemplates.OrderBy(m => m.Name).ToListAsync();

    public Task<MonsterTemplate?> FindMonsterAsync(string name)
    {
        var trimmed = name.Trim();
        return _context.MonsterTemplates.FirstOrDefaultAsync(m => m.Name == trimmed);
    }

    public async Task SaveMonsterAsync(MonsterTemplate monster)
    {
        if (_context.Entry(monster).State == EntityState.Detached)
            _context.MonsterTemplates.Add(monster);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveMonsterAsync(MonsterTemplate monster)
    {
        _context.MonsterTemplates.Remove(monster);
        await _context.SaveChangesAsync();
    }
}

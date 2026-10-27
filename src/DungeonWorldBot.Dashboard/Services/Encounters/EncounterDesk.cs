using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Encounters;

/// <summary>
/// The other part of the bot's database the dashboard writes, besides <see cref="Party.CharacterJournal"/> and
/// <see cref="Npcs.NpcDesk"/>: the DM starting, filling and ending a fight from its scene, looking after the enemies in
/// fights going on, and keeping the saved monsters. The bot's own rules do the work, and the bot announces the
/// start and end in game chat when it notices them.
/// </summary>
public class EncounterDesk
{
    public const int MaxNameLength = FormText.MaxNameLength;
    public const int MaxTextLength = FormText.MaxTextLength;
    public const int MaxCount = 20;

    private readonly DbContextOptions<DungeonWorldContext> _options;

    /// <param name="options">Options for opening the bot's database read-write.</param>
    public EncounterDesk(DbContextOptions<DungeonWorldContext> options)
    {
        _options = options;
    }

    /// <summary>
    /// Starts a fight on the scene with who the DM picked, and copies of saved monsters already in it.
    /// </summary>
    /// <param name="monsters">How many of each saved monster to start with, by name.</param>
    public async Task<Added> StartAsync(string? name, SceneLink scene, EncounterRoster roster, IReadOnlyList<MonsterCount> monsters)
    {
        if (FormText.Name(name) is not { } fightName)
            return Added.Failed($"Give the fight a name, up to {MaxNameLength} letters.");
        if (monsters.FirstOrDefault(m => m.Count is < 0 or > MaxCount) is { } tooMany)
            return Added.Failed($"Start with between 0 and {MaxCount} of {tooMany.Name}.");

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);

        // Every monster checked before the fight starts, so a mistake doesn't leave it half set up
        var templates = new Dictionary<MonsterTemplate, int>();
        foreach (var monster in monsters.Where(m => m.Count > 0))
        {
            if (await service.FindMonsterAsync(monster.Name) is not { } template)
                return Added.Failed($"There's no saved monster called {monster.Name.Trim()}.");
            templates[template] = monster.Count;
        }

        if (await service.GetOnSceneAsync(scene.ID) is { } onScene)
            return Added.Failed($"{onScene.Name} is still going on here. End it first.");
        if (await service.FindRunningAsync(fightName) is { } sameName)
            return Added.Failed($"{sameName.Name} is already going on. Give this fight another name.");
        if (await service.StartAsync(fightName, scene, roster: roster) is not { } encounter)
            return Added.Failed("Couldn't start the fight. Try again.");

        var spawned = new List<Enemy>();
        foreach (var (template, count) in templates)
            spawned.AddRange(await service.SpawnAsync(encounter, template, count));

        return new Added(null, spawned);
    }

    /// <summary>
    /// Copies of a saved monster into the scene's fight.
    /// </summary>
    public async Task<Added> AddMonsterAsync(string sceneID, string? monster, int count)
    {
        if (string.IsNullOrWhiteSpace(monster))
            return Added.Failed("Pick a monster.");
        if (count is < 1 or > MaxCount)
            return Added.Failed($"Add between 1 and {MaxCount} at a time.");

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightOnAsync(service, sceneID) is not { } encounter)
            return Added.Failed(NoFight);
        if (await service.FindMonsterAsync(monster) is not { } template)
            return Added.Failed($"There's no saved monster called {monster.Trim()}.");

        var spawned = await service.SpawnAsync(encounter, template, count);
        return new Added(null, spawned.ToList());
    }

    /// <summary>
    /// A new enemy into the scene's fight, also saved as a monster for later fights if asked.
    /// </summary>
    public async Task<Added> AddCustomAsync(string sceneID, MonsterTemplate details, bool save)
    {
        if (FormText.Name(details.Name) is not { } name)
            return Added.Failed($"Give it a name, up to {MaxNameLength} letters.");
        if (Problem(details.MaxHP, details.Armor) is { } problem)
            return Added.Failed(problem);

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightOnAsync(service, sceneID) is not { } encounter)
            return Added.Failed(NoFight);
        if (await service.FindEnemyAsync(name) is { } taken)
            return Added.Failed($"{taken.Name} is already in a fight.");
        if (save && await service.FindMonsterAsync(name) is { } existing)
            return Added.Failed($"A monster called {existing.Name} is already saved. Add it from the saved monsters, or untick saving.");

        var text = Descriptions.Of(details);

        var enemy = new Enemy { Name = name, CurrentHP = details.MaxHP, MaxHP = details.MaxHP, Armor = details.Armor };
        text.ApplyTo(enemy);
        await service.AddEnemyAsync(encounter, enemy);

        if (save)
        {
            var monster = new MonsterTemplate { Name = name, MaxHP = details.MaxHP, Armor = details.Armor };
            text.ApplyTo(monster);
            await service.SaveMonsterAsync(monster);
        }

        return new Added(null, [enemy]);
    }

    /// <summary>
    /// Asks the bot to roll a move made from a scene (see <see cref="MoveRequest"/>). Refused while whoever's making it
    /// is still waiting on their last one, so a double-click can't make it twice.
    /// </summary>
    public async Task<MoveAsked> RequestMoveAsync(MoveRequest request)
    {
        await using var context = new DungeonWorldContext(_options);

        // SQLite can't compare times, so the few waiting are checked here
        var waiting = await context.MoveRequests
            .Where(r => r.ResolvedAt == null && r.EncounterID == request.EncounterID)
            .Where(r => request.CharacterID != null ? r.CharacterID == request.CharacterID : r.EnemyID == request.EnemyID)
            .Select(r => r.RequestedAt)
            .ToListAsync();
        if (waiting.Any(at => DateTimeOffset.UtcNow - at < MoveRequestWorker.GiveUpAfter))
            return MoveAsked.Failed("Still waiting on the last move. Give the bot a moment.");

        request.RequestedAt = DateTimeOffset.UtcNow;
        context.MoveRequests.Add(request);
        await context.SaveChangesAsync();
        return new MoveAsked(null, request.ID);
    }

    /// <summary>
    /// Ends the scene's fight. The bot clears temporary armor and announces it.
    /// </summary>
    public async Task<string?> EndAsync(string sceneID)
    {
        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightOnAsync(service, sceneID) is not { } encounter)
            return NoFight;

        await service.EndAsync(encounter);
        return null;
    }

    /// <summary>
    /// Hurts or heals an enemy in a fight going on. Damage is reduced by its armor.
    /// </summary>
    public async Task<string?> ChangeEnemyHpAsync(int enemyID, HpChange change, int amount)
    {
        if (amount < 1)
            return "Give an amount of at least 1.";

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightingAsync(context, enemyID) is not { } enemy)
            return NoEnemy;

        if (change == HpChange.Damage)
            await service.DamageAsync(enemy, amount);
        else
            await service.HealAsync(enemy, amount);
        return null;
    }

    /// <summary>
    /// Changes an enemy in a fight going on. Its name stays, since its tokens on the board go by it.
    /// </summary>
    public async Task<string?> EditEnemyAsync(int enemyID, Enemy details)
    {
        if (Problem(details.MaxHP, details.Armor) is { } problem)
            return problem;

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightingAsync(context, enemyID) is not { } enemy)
            return NoEnemy;

        enemy.MaxHP = details.MaxHP;
        enemy.CurrentHP = details.CurrentHP;
        enemy.Armor = details.Armor;
        Descriptions.Of(details).ApplyTo(enemy);
        // Keeps current HP between 0 and the max
        await service.SaveAsync(enemy);
        return null;
    }

    /// <summary>
    /// Takes an enemy out of its fight. What it did stays in the fight's spotlight, under its name.
    /// </summary>
    public async Task<string?> RemoveEnemyAsync(int enemyID)
    {
        await using var context = new DungeonWorldContext(_options);
        if (await FightingAsync(context, enemyID) is not { } enemy)
            return NoEnemy;

        await new EncounterService(context).RemoveAsync(enemy);
        return null;
    }

    /// <summary>
    /// Saves a new monster for later fights, or changes one already saved. Enemies already spawned from it stay as they are.
    /// </summary>
    /// <param name="monsterID">The monster to change, or null for a new one.</param>
    public async Task<string?> SaveMonsterAsync(int? monsterID, MonsterTemplate details)
    {
        if (FormText.Name(details.Name) is not { } name)
            return $"Give it a name, up to {MaxNameLength} letters.";
        if (Problem(details.MaxHP, details.Armor) is { } problem)
            return problem;

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);

        MonsterTemplate monster;
        if (monsterID is null)
            monster = new MonsterTemplate();
        else if (await context.MonsterTemplates.FirstOrDefaultAsync(m => m.ID == monsterID) is { } found)
            monster = found;
        else
            return NoMonster;

        if (await service.FindMonsterAsync(name) is { } taken && taken.ID != monster.ID)
            return $"A monster called {taken.Name} is already saved.";

        monster.Name = name;
        monster.MaxHP = details.MaxHP;
        monster.Armor = details.Armor;
        Descriptions.Of(details).ApplyTo(monster);
        await service.SaveMonsterAsync(monster);
        return null;
    }

    public async Task<string?> RemoveMonsterAsync(int monsterID)
    {
        await using var context = new DungeonWorldContext(_options);
        if (await context.MonsterTemplates.FirstOrDefaultAsync(m => m.ID == monsterID) is not { } monster)
            return NoMonster;

        await new EncounterService(context).RemoveMonsterAsync(monster);
        return null;
    }

    private const string NoFight = "There's no fight going on here.";
    private const string NoEnemy = "That enemy isn't in a fight going on any more.";
    private const string NoMonster = "That monster isn't saved any more.";

    /// <summary>
    /// What's wrong with an enemy's or monster's numbers, if anything.
    /// </summary>
    private static string? Problem(int maxHP, int armor) =>
        maxHP < 1 ? "Give it at least 1 HP."
        : armor < 0 ? "Armor can't be negative."
        : null;

    /// <summary>
    /// An enemy by ID, if it's in a fight still going on. Ended fights are history, so they're left alone.
    /// </summary>
    private static Task<Enemy?> FightingAsync(DungeonWorldContext context, int enemyID) =>
        context.Enemies.FirstOrDefaultAsync(e => e.ID == enemyID && e.Encounter.EndedAt == null);

    /// <summary>
    /// The fight going on on this scene.
    /// </summary>
    private static Task<Encounter?> FightOnAsync(EncounterService service, string sceneID) => service.GetOnSceneAsync(sceneID);

    /// <summary>
    /// The free text an enemy or monster is described with, tidied from what the DM typed.
    /// </summary>
    private record Descriptions(string? Damage, string? Tags, string? Instinct, string? Moves, string? Notes)
    {
        public static Descriptions Of(MonsterTemplate typed) =>
            Clean(typed.Damage, typed.Tags, typed.Instinct, typed.Moves, typed.Notes);

        public static Descriptions Of(Enemy typed) =>
            Clean(typed.Damage, typed.Tags, typed.Instinct, typed.Moves, typed.Notes);

        private static Descriptions Clean(string? damage, string? tags, string? instinct, string? moves, string? notes) => new(
            FormText.Clean(damage, MaxNameLength),
            FormText.Clean(tags),
            FormText.Clean(instinct),
            FormText.Clean(moves),
            FormText.Clean(notes));

        public void ApplyTo(Enemy enemy)
        {
            enemy.Damage = Damage;
            enemy.Tags = Tags;
            enemy.Instinct = Instinct;
            enemy.Moves = Moves;
            enemy.Notes = Notes;
        }

        public void ApplyTo(MonsterTemplate monster)
        {
            monster.Damage = Damage;
            monster.Tags = Tags;
            monster.Instinct = Instinct;
            monster.Moves = Moves;
            monster.Notes = Notes;
        }
    }
}

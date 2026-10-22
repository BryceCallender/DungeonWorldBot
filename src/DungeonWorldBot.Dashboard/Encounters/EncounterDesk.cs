using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// The other part of the bot's database the dashboard writes, besides <see cref="Party.CharacterJournal"/>: the DM
/// starting, filling and ending a fight from its scene. The bot's own rules do the work, and the bot announces the
/// start and end in game chat when it notices them.
/// </summary>
/// <param name="Error">Why it couldn't, or null once it has.</param>
/// <param name="Added">The names of the enemies added.</param>
public sealed record Added(string? Error, IReadOnlyList<string> Names)
{
    public static Added Failed(string error) => new(error, []);
}

public sealed class EncounterDesk
{
    public const int MaxNameLength = 80;
    public const int MaxTextLength = 1000;
    public const int MaxCount = 20;

    private readonly DbContextOptions<DungeonWorldContext> _options;

    /// <param name="options">Options for opening the bot's database read-write.</param>
    public EncounterDesk(DbContextOptions<DungeonWorldContext> options)
    {
        _options = options;
    }

    /// <returns>Why it couldn't, or null once it has.</returns>
    public async Task<string?> StartAsync(string? name, SceneLink scene)
    {
        if (Name(name) is not { } fightName)
            return $"Give the fight a name, up to {MaxNameLength} letters.";

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        return await service.StartAsync(fightName, scene) is null
            ? $"{(await service.GetActiveAsync())?.Name ?? "Another fight"} is still going. End it first."
            : null;
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
        return new Added(null, spawned.Select(e => e.Name).ToList());
    }

    /// <summary>
    /// A new enemy into the scene's fight, also saved as a monster for later fights if asked.
    /// </summary>
    public async Task<Added> AddCustomAsync(string sceneID, MonsterTemplate details, bool save)
    {
        if (Name(details.Name) is not { } name)
            return Added.Failed($"Give it a name, up to {MaxNameLength} letters.");
        if (details.MaxHP < 1)
            return Added.Failed("Give it at least 1 HP.");
        if (details.Armor < 0)
            return Added.Failed("Armor can't be negative.");

        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightOnAsync(service, sceneID) is not { } encounter)
            return Added.Failed(NoFight);
        if (await service.FindEnemyAsync(name) is { } taken)
            return Added.Failed($"{taken.Name} is already in the fight.");
        if (save && await service.FindMonsterAsync(name) is { } existing)
            return Added.Failed($"A monster called {existing.Name} is already saved. Add it from the saved monsters, or untick saving.");

        var damage = Clean(details.Damage, MaxNameLength);
        var tags = Clean(details.Tags, MaxTextLength);
        var instinct = Clean(details.Instinct, MaxTextLength);
        var moves = Clean(details.Moves, MaxTextLength);
        var notes = Clean(details.Notes, MaxTextLength);

        await service.AddEnemyAsync(encounter, new Enemy
        {
            Name = name,
            CurrentHP = details.MaxHP,
            MaxHP = details.MaxHP,
            Armor = details.Armor,
            Damage = damage,
            Tags = tags,
            Instinct = instinct,
            Moves = moves,
            Notes = notes,
        });

        if (save)
        {
            await service.SaveMonsterAsync(new MonsterTemplate
            {
                Name = name,
                MaxHP = details.MaxHP,
                Armor = details.Armor,
                Damage = damage,
                Tags = tags,
                Instinct = instinct,
                Moves = moves,
                Notes = notes,
            });
        }

        return new Added(null, [name]);
    }

    /// <summary>
    /// Ends the scene's fight. The bot clears temporary armor and announces it.
    /// </summary>
    public async Task<string?> EndAsync(string sceneID)
    {
        await using var context = new DungeonWorldContext(_options);
        var service = new EncounterService(context);
        if (await FightOnAsync(service, sceneID) is null)
            return NoFight;

        await service.EndAsync();
        return null;
    }

    private const string NoFight = "There's no fight going on here.";

    /// <summary>
    /// The fight going on, if it's on this scene or on no scene in particular.
    /// </summary>
    private static async Task<Encounter?> FightOnAsync(EncounterService service, string sceneID) =>
        await service.GetActiveAsync() is { } encounter && (encounter.SceneID is null || encounter.SceneID == sceneID)
            ? encounter
            : null;

    /// <summary>
    /// A name: trimmed, or null when it's blank or too long to use.
    /// </summary>
    private static string? Name(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength ? null : trimmed;
    }

    /// <summary>
    /// Optional text: trimmed, null when blank, and cut to length (the form already stops longer).
    /// </summary>
    private static string? Clean(string? value, int maxLength)
    {
        // Text boxes send Windows line endings
        var trimmed = value?.Replace("\r\n", "\n").Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

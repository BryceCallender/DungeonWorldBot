using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// A dashboard scene a fight is on.
/// </summary>
/// <param name="ID">Its short ID, as in its address.</param>
/// <param name="Url">Its full address, for links in Discord.</param>
public sealed record SceneLink(string ID, string Name, string? Url);

public interface IEncounterService
{
    /// <summary>
    /// The fight going on now, with its enemies, or null between fights.
    /// </summary>
    Task<Encounter?> GetActiveAsync();

    /// <summary>
    /// Starts a fight, unless one is already going.
    /// </summary>
    /// <param name="scene">The dashboard scene it's on, if any.</param>
    /// <param name="announced">Whether game chat has been told already, as by a command's reply. Otherwise the bot
    /// announces it when it notices.</param>
    /// <returns>The new fight, or null if one is already going.</returns>
    Task<Encounter?> StartAsync(string name, SceneLink? scene = null, bool announced = false);

    /// <summary>
    /// Ends the fight going on now.
    /// </summary>
    /// <param name="announced">Whether game chat has been told already and temporary armor cleared, as by
    /// /dm encounter end. Otherwise the bot does both when it notices.</param>
    /// <returns>The fight that ended, or null if there wasn't one.</returns>
    Task<Encounter?> EndAsync(bool announced = false);

    /// <summary>
    /// Fights whose start or end game chat hasn't heard about yet, oldest first.
    /// </summary>
    Task<List<Encounter>> GetUnannouncedAsync();

    /// <summary>
    /// Saves changes made to a fight's own fields.
    /// </summary>
    Task SaveAsync(Encounter encounter);

    /// <summary>
    /// An enemy in the current fight by name, whatever its capitalization.
    /// </summary>
    Task<Enemy?> FindEnemyAsync(string name);

    /// <summary>
    /// Adds an enemy to the current fight.
    /// </summary>
    Task AddEnemyAsync(Encounter encounter, Enemy enemy);

    /// <summary>
    /// Adds copies of a monster to the current fight, numbered after any already there ("Goblin 1", "Goblin 2"). One
    /// copy in a fight without any keeps the plain name.
    /// </summary>
    Task<IReadOnlyList<Enemy>> SpawnAsync(Encounter encounter, MonsterTemplate monster, int count = 1);

    /// <summary>
    /// Saves changes made to an enemy's fields.
    /// </summary>
    Task SaveAsync(Enemy enemy);

    Task RemoveAsync(Enemy enemy);

    /// <summary>
    /// Damages an enemy. Their armor reduces physical damage, less whatever the hit pierces; other types go straight through.
    /// </summary>
    Task<ArmoredHit> DamageAsync(Enemy enemy, int amount, DamageType type = DamageType.Physical, int piercing = 0);

    /// <summary>
    /// Heals an enemy, up to their max HP. Leave the amount out to heal them fully.
    /// </summary>
    Task HealAsync(Enemy enemy, int? amount = null);

    Task<List<MonsterTemplate>> GetMonstersAsync();

    /// <summary>
    /// A saved monster by name, whatever its capitalization.
    /// </summary>
    Task<MonsterTemplate?> FindMonsterAsync(string name);

    /// <summary>
    /// Saves a new monster, or changes to one already saved.
    /// </summary>
    Task SaveMonsterAsync(MonsterTemplate monster);

    Task RemoveMonsterAsync(MonsterTemplate monster);
}

using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// A dashboard scene a fight is on.
/// </summary>
/// <param name="ID">Its short ID, as in its address.</param>
/// <param name="Url">Its full address, for links in Discord.</param>
public record SceneLink(string ID, string Name, string? Url);

/// <summary>
/// Who the DM picked to be in a fight.
/// </summary>
/// <param name="Characters">The players' characters, by their players' Discord IDs.</param>
/// <param name="Npcs">The NPCs, by ID.</param>
public record EncounterRoster(IReadOnlyList<Snowflake> Characters, IReadOnlyList<int> Npcs);

public interface IEncounterService
{
    /// <summary>
    /// The fights going on now, with their enemies and who's in them, oldest first.
    /// </summary>
    Task<List<Encounter>> GetRunningAsync();

    /// <summary>
    /// The fight going on on a dashboard scene, by the scene's short ID, or null if there isn't one.
    /// </summary>
    Task<Encounter?> GetOnSceneAsync(string sceneID);

    /// <summary>
    /// A fight going on now by name, whatever its capitalization.
    /// </summary>
    Task<Encounter?> FindRunningAsync(string name);

    /// <summary>
    /// The fight a player's character or an NPC is in: the newest the DM picked them for, else the newest open to
    /// everyone, else the only fight going on.
    /// </summary>
    Task<Encounter?> GetForAsync(Snowflake? characterID = null, int? npcID = null);

    /// <summary>
    /// The characters and NPCs the DM picked for fights going on. They can only be in one at a time.
    /// </summary>
    Task<EncounterRoster> GetTakenAsync();

    /// <summary>
    /// Starts a fight. Several can go at once, each with its own name, and one at a time on a scene.
    /// </summary>
    /// <param name="scene">The dashboard scene it's on, if any.</param>
    /// <param name="announced">Whether game chat has been told already, as by a command's reply. Otherwise the bot
    /// announces it when it notices.</param>
    /// <param name="roster">Who's in it. Everyone, if not given. Anyone already picked for another fight is left out.</param>
    /// <returns>The new fight, or null if one by that name or on that scene is already going.</returns>
    Task<Encounter?> StartAsync(string name, SceneLink? scene = null, bool announced = false, EncounterRoster? roster = null);

    /// <summary>
    /// Ends a fight.
    /// </summary>
    /// <param name="announced">Whether game chat has been told already and temporary armor cleared, as by
    /// /dm encounter end. Otherwise the bot does both when it notices.</param>
    Task EndAsync(Encounter encounter, bool announced = false);

    /// <summary>
    /// The characters whose temporary armor goes when a fight ends: those the DM picked for it, or for a fight open to
    /// everyone, everyone not picked for another fight still going.
    /// </summary>
    Task<IReadOnlyList<Snowflake>> LeavingAsync(Encounter ended);

    /// <summary>
    /// Fights whose start or end game chat hasn't heard about yet, oldest first.
    /// </summary>
    Task<List<Encounter>> GetUnannouncedAsync();

    /// <summary>
    /// Saves changes made to a fight's own fields.
    /// </summary>
    Task SaveAsync(Encounter encounter);

    /// <summary>
    /// An enemy in a fight going on by name, whatever its capitalization. Names are unique across the fights going on.
    /// </summary>
    Task<Enemy?> FindEnemyAsync(string name);

    /// <summary>
    /// Adds an enemy to a fight.
    /// </summary>
    Task AddEnemyAsync(Encounter encounter, Enemy enemy);

    /// <summary>
    /// Adds copies of a monster to a fight, numbered after any already in the fights going on ("Goblin 1", "Goblin 2"),
    /// so each name is unique across them. One copy when there's none anywhere keeps the plain name.
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

    /// <summary>
    /// Notes something done in a fight, for the spotlight: an enemy's in its own fight, and anyone else's in the fight
    /// of the enemy they aimed at, or else the one they're in (see <see cref="GetForAsync"/>). Does nothing when
    /// there's no such fight.
    /// </summary>
    Task LogAsync(EncounterAction action);

    /// <summary>
    /// Everything done in a fight, oldest first.
    /// </summary>
    Task<List<EncounterAction>> GetActionsAsync(Encounter encounter);

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

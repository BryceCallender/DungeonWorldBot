using DungeonWorldBot.Dashboard.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// Someone on a fight's replay: a character, an NPC or an enemy.
/// </summary>
/// <param name="Key">Who they are, like "enemy:12", matching the steps' actors and targets.</param>
/// <param name="Side">"party" or "enemies".</param>
/// <param name="MaxHP">Their max HP, so their health can go down as they're hurt. Null when it isn't known, like for
/// someone since taken out of the fight.</param>
/// <param name="Color">A character's class colour, light and dark.</param>
public record ReplayFighter(string Key, string Name, string Side, string? Portrait, int? MaxHP, ClassColor? Color);

/// <summary>
/// One thing someone did in the fight, in order.
/// </summary>
/// <param name="At">How far into the fight, like "4m".</param>
/// <param name="Damage">What it did to the target, from its result ("💥 4 damage").</param>
public record ReplayStep(string At, string? Actor, string ActorName, string Action, string? Roll, string? Target, string? TargetName, string? Result, int Damage);

public record FightReplay(Encounter Fight, IReadOnlyList<ReplayFighter> Fighters, IReadOnlyList<ReplayStep> Steps);

/// <summary>
/// A fight's timeline, put back together for watching again: who was in it and everything they did. Enemies start at
/// their max HP and lose what each hit did, which is how the replay shows them falling.
/// </summary>
public class FightReplayReader(DungeonWorldContext context, PartyReader party, PortraitStore portraits)
{
    /// <summary>
    /// The last fight that finished on a scene, by the scene's short ID, for replaying from it.
    /// </summary>
    public async Task<Encounter?> GetLastEndedOnSceneAsync(string sceneID)
    {
        try
        {
            return await context.Encounters.AsNoTracking()
                .Where(e => e.SceneID == sceneID && e.EndedAt != null && e.Actions.Any())
                .OrderByDescending(e => e.ID)
                .FirstOrDefaultAsync();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            return null;
        }
    }

    /// <summary>
    /// How much damage each enemy has taken in a fight, by name, as its timeline tells it: what players can already
    /// read there, unlike the enemies' health.
    /// </summary>
    public async Task<Dictionary<string, int>> DamageTakenAsync(int fightID)
    {
        try
        {
            return (await context.EncounterActions.AsNoTracking()
                    .Where(a => a.EncounterID == fightID && a.Target != null && a.Result != null)
                    .Select(a => new { a.Target, a.Result })
                    .ToListAsync())
                .GroupBy(a => a.Target!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(a => FightDamage.Of(a.Result)), StringComparer.OrdinalIgnoreCase);
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task<FightReplay?> GetAsync(int fightID)
    {
        Encounter? fight;
        try
        {
            fight = await context.Encounters.AsNoTracking()
                .Include(e => e.Enemies)
                .Include(e => e.Actions)
                .FirstOrDefaultAsync(e => e.ID == fightID);
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            return null;
        }

        if (fight is null)
            return null;

        var characters = await party.GetPartyAsync();
        var npcs = await party.GetNpcsAsync();
        var pictures = await portraits.UrlsAsync();
        var actions = fight.Actions.OrderBy(a => a.ID).ToList();

        var fighters = new List<ReplayFighter>();
        ReplayFighter Add(ReplayFighter f)
        {
            if (fighters.FirstOrDefault(x => x.Key == f.Key) is { } known)
                return known;
            fighters.Add(f);
            return f;
        }

        ReplayFighter Character(ulong id, string fallback) =>
            characters.FirstOrDefault(c => c.Character.ID.Value == id) is { } c
                ? Add(new ReplayFighter($"character:{id}", c.Name, "party", pictures.GetValueOrDefault(id), c.Character.Health?.MaxHP, ClassSheetLibrary.For(c.Character.Class.Type)?.Color))
                : Add(new ReplayFighter($"character:{id}", fallback, "party", pictures.GetValueOrDefault(id), null, null));

        // Everyone who acted or was aimed at, enemies in the fight first so they keep their order
        foreach (var enemy in fight.Enemies.OrderBy(e => e.ID))
            Add(new ReplayFighter($"enemy:{enemy.ID}", enemy.Name, "enemies", null, enemy.MaxHP, null));

        ReplayFighter Actor(EncounterAction a) =>
            a.CharacterID is { } cid ? Character(cid.Value, a.Actor)
            : a.EnemyID is { } eid ? Add(new ReplayFighter($"enemy:{eid}", a.Actor, "enemies", null, null, null))
            : a.NpcID is { } nid ? Npc(nid, a.Actor)
            // Someone since taken out of the fight: they're found by name, or else they were an enemy
            : ByName(a.Actor) ?? Add(new ReplayFighter($"name:{a.Actor.ToLowerInvariant()}", a.Actor, "enemies", null, null, null));

        ReplayFighter? ByName(string name) =>
            fighters.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? (characters.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } c ? Character(c.Character.ID.Value, c.Name) : null)
            ?? (npcs.FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } n ? Npc(n.ID, n.Name) : null);

        ReplayFighter Npc(int id, string fallback) =>
            npcs.FirstOrDefault(n => n.ID == id) is { } n
                ? Add(new ReplayFighter($"npc:{id}", n.Name, "party", null, n.MaxHP, null))
                : Add(new ReplayFighter($"npc:{id}", fallback, "party", null, null, null));

        var steps = actions.Select(a =>
        {
            var actor = Actor(a);
            var target = a.Target is { } t
                ? ByName(t) ?? Add(new ReplayFighter($"name:{t.ToLowerInvariant()}", t, actor.Side == "party" ? "enemies" : "party", null, null, null))
                : null;
            var into = a.At - fight.StartedAt;
            return new ReplayStep(
                into.TotalHours >= 1 ? $"{(int)into.TotalHours}h {into.Minutes:00}m" : $"{Math.Max(0, (int)into.TotalMinutes)}m",
                actor.Key, actor.Name, a.Action, a.Roll, target?.Key, target?.Name, a.Result, FightDamage.Of(a.Result));
        }).ToList();

        return new FightReplay(fight, fighters, steps);
    }
}

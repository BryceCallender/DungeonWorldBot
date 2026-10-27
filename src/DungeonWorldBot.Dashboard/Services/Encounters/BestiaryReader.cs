using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Encounters;

public class BestiaryReader(DungeonWorldContext context)
{
    public async Task<Bestiary> GetAsync()
    {
        List<Enemy> enemies;
        List<EncounterAction> actions;
        List<MonsterTemplate> templates;
        try
        {
            enemies = await context.Enemies.AsNoTracking().Include(e => e.Encounter).ToListAsync();
            actions = await context.EncounterActions.AsNoTracking().Where(a => a.Result != null).ToListAsync();
            templates = await context.MonsterTemplates.AsNoTracking().ToListAsync();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            // The bot hasn't added fights to its database yet
            return new Bestiary([], null);
        }

        var dealtBy = actions
            .Where(a => a.EnemyID is not null)
            .GroupBy(a => FightDamage.Kind(a.Actor), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(a => FightDamage.Of(a.Result)), StringComparer.OrdinalIgnoreCase);

        var takenBy = actions
            .Where(a => a.EnemyID is null && a.Target is not null)
            .GroupBy(a => FightDamage.Kind(a.Target!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var entries = enemies
            .GroupBy(e => FightDamage.Kind(e.Name), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var hits = takenBy.GetValueOrDefault(g.Key) ?? [];
                var slayer = hits
                    .Where(a => a.CharacterID is not null)
                    .GroupBy(a => a.CharacterID!.Value.Value)
                    .Select(s => new { Player = s.Key, Damage = s.Sum(a => FightDamage.Of(a.Result)) })
                    .Where(s => s.Damage > 0)
                    .MaxBy(s => s.Damage);
                return new BestiaryEntry(
                    g.Key,
                    g.Count(),
                    g.Count(e => e.CurrentHP <= 0),
                    g.Select(e => e.EncounterID).Distinct().Count(),
                    g.Min(e => e.Encounter.StartedAt),
                    dealtBy.GetValueOrDefault(g.Key),
                    hits.Sum(a => FightDamage.Of(a.Result)),
                    slayer?.Player,
                    slayer?.Damage ?? 0,
                    templates.FirstOrDefault(t => t.Name.Equals(g.Key, StringComparison.OrdinalIgnoreCase)));
            })
            .OrderByDescending(e => e.Slain)
            .ThenByDescending(e => e.Met)
            .ThenBy(e => e.Name)
            .ToList();

        return new Bestiary(entries, entries.Where(e => e.DamageToParty > 0).MaxBy(e => e.DamageToParty));
    }
}

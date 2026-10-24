using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

[Scoped<ISpotlightService>]
public class SpotlightService : ISpotlightService
{
    private const string PlainRoll = "Roll";

    private readonly DungeonWorldContext _context;
    private readonly IRollStatsService _rollStats;
    private readonly IEncounterService _encounterService;
    private readonly INpcService _npcService;

    public SpotlightService(
        DungeonWorldContext context,
        IRollStatsService rollStats,
        IEncounterService encounterService,
        INpcService npcService)
    {
        _context = context;
        _rollStats = rollStats;
        _encounterService = encounterService;
        _npcService = npcService;
    }

    public async Task<Spotlight> GetAsync(Encounter encounter, IEnumerable<Character> party)
    {
        var log = await _encounterService.GetActionsAsync(encounter);
        var rolls = _rollStats.GameChatChannelID is { } gameChat
            ? (await _context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync())
                .Where(r => r.MessageID.Timestamp >= encounter.StartedAt)
                .OrderBy(r => r.MessageID.Value)
                .ToList()
            : [];

        // A move logged in the fight is also a roll kept for stats; the log says more about it
        var logged = log.Where(a => a.MessageID is not null).ToDictionary(a => a.MessageID!.Value);
        var rolled = rolls.Select(r => r.MessageID).ToHashSet();

        var players = party
            .Where(c => c.Status != Status.Dead)
            .Select(c =>
            {
                var actions = rolls
                    .Where(r => r.UserID == c.ID)
                    .Select(r => logged.TryGetValue(r.MessageID, out var action)
                        ? FromLog(action, r.MessageID.Timestamp, IsMiss(r))
                        : FromRoll(r))
                    .Concat(log
                        .Where(a => a.CharacterID == c.ID && (a.MessageID is not { } id || !rolled.Contains(id)))
                        .Select(a => FromLog(a, a.At, missed: false)))
                    .OrderBy(a => a.At)
                    .ToList();
                return new Actor(c.Name, c.ID, actions);
            })
            .ToList();

        var allies = (await _npcService.GetAllAsync())
            .Where(n => n.CurrentHP > 0)
            .Select(n => new Actor(n.Name, null, log.Where(a => a.NpcID == n.ID).Select(a => FromLog(a, a.At, missed: false)).ToList()))
            .ToList();

        var enemies = encounter.Enemies
            .Where(e => e.CurrentHP > 0)
            .Select(e => new Actor(e.Name, null, log.Where(a => a.EnemyID == e.ID).Select(a => FromLog(a, a.At, missed: false)).ToList()))
            .ToList();

        var partyActions = players.SelectMany(p => p.Actions).ToList();
        return new Spotlight(Lines(players), Lines(allies), Lines(enemies), partyActions.Count, partyActions.Count(a => a.Missed));
    }

    /// <summary>
    /// Someone in the fight with everything they've done in it, oldest first.
    /// </summary>
    private record Actor(string Name, Snowflake? CharacterID, IReadOnlyList<SpotlightAction> Actions)
    {
        public DateTimeOffset? LastAt => Actions is [.., var last] ? last.At : null;
    }

    // Quietest first
    private static List<SpotlightLine> Lines(List<Actor> side)
    {
        var anyActed = side.Any(a => a.Actions.Count > 0);
        return side
            .Select(actor =>
            {
                var others = side.Where(o => o != actor).ToList();
                // Before anyone acts, nobody's been left out yet
                var quiet = actor.LastAt is not { } last
                    ? anyActed
                    : others.Count > 0 && others.All(o => o.LastAt > last);

                return new SpotlightLine(
                    actor.Name,
                    actor.CharacterID,
                    actor.Actions.Count,
                    actor.Actions.Count(a => a.Missed),
                    actor.Actions.LastOrDefault(),
                    quiet);
            })
            .OrderBy(l => l.Last?.At ?? DateTimeOffset.MinValue)
            .ThenBy(l => l.Name)
            .ToList();
    }

    private static SpotlightAction FromLog(EncounterAction action, DateTimeOffset at, bool missed) =>
        new(at, action.Action, action.Roll, action.Target, action.Result, missed);

    private static SpotlightAction FromRoll(RollRecord roll)
    {
        var outcome = new MoveDice(roll.First, roll.Second, roll.Total).Outcome;
        return new SpotlightAction(
            roll.MessageID.Timestamp, roll.Move ?? PlainRoll, $"{roll.Total} · {MoveRoller.Describe(outcome)}", null, null, outcome == MoveOutcome.Miss);
    }

    private static bool IsMiss(RollRecord roll) => new MoveDice(roll.First, roll.Second, roll.Total).Outcome == MoveOutcome.Miss;
}

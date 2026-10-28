using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

[Scoped<ISpotlightService>]
public class SpotlightService(IFightLogService fightLog, INpcService npcService) : ISpotlightService
{
    private const string PlainRoll = "Roll";

    public async Task<Spotlight> GetAsync(Encounter encounter, IEnumerable<Character> party)
    {
        var fight = await fightLog.GetAsync(encounter);
        var log = fight.Actions;
        var rolls = fight.Rolls;
        var logged = fight.Logged;
        var rolled = fight.Rolled;

        var players = party
            .Where(c => c.Status != Status.Dead)
            .Select(c =>
            {
                var actions = rolls
                    .Where(r => r.UserID == c.ID)
                    .Select(r => logged.TryGetValue(r.MessageID, out var action)
                        ? FromLog(action, r.MessageID.Timestamp, IsMiss(r), r.ChangedReason)
                        : FromRoll(r))
                    .Concat(log
                        .Where(a => a.CharacterID == c.ID && (a.MessageID is not { } id || !rolled.Contains(id)))
                        .Select(a => FromLog(a, a.At, missed: false)))
                    .OrderBy(a => a.At)
                    .ToList();
                return new Actor(c.Name, c.ID, actions);
            })
            // Whoever the DM picked, and anyone who joined in anyway
            .Where(a => InFight(encounter, a))
            .ToList();

        var allies = (await npcService.GetAllAsync())
            .Where(n => n.CurrentHP > 0)
            .Select(n => new Actor(n.Name, null, log.Where(a => a.NpcID == n.ID).Select(a => FromLog(a, a.At, missed: false)).ToList(), n.ID))
            .Where(a => InFight(encounter, a))
            .ToList();

        var enemies = encounter.Enemies
            .Where(e => e.CurrentHP > 0)
            .Select(e => new Actor(e.Name, null, log.Where(a => a.EnemyID == e.ID).Select(a => FromLog(a, a.At, missed: false)).ToList()))
            .ToList();

        var partyActions = players.SelectMany(p => p.Actions).ToList();
        return new Spotlight(Lines(players), Lines(allies), Lines(enemies), partyActions.Count, partyActions.Count(a => a.Missed));
    }

    /// <summary>
    /// Whether they're in the fight: picked by the DM (everyone is, in one the DM didn't pick for), or joined in anyway.
    /// </summary>
    private static bool InFight(Encounter encounter, Actor actor) =>
        encounter.Participants.Count == 0
        || actor.Actions.Count > 0
        || encounter.Participants.Any(p =>
            (actor.CharacterID is not null && p.CharacterID == actor.CharacterID) || (actor.NpcID is not null && p.NpcID == actor.NpcID));

    /// <summary>
    /// Someone in the fight with everything they've done in it, oldest first.
    /// </summary>
    private record Actor(string Name, Snowflake? CharacterID, IReadOnlyList<SpotlightAction> Actions, int? NpcID = null)
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

    /// <param name="changedReason">Why the roll's outcome was changed, when the action was a roll that was.</param>
    internal static SpotlightAction FromLog(EncounterAction action, DateTimeOffset at, bool missed, string? changedReason = null) =>
        new(at, action.Action, action.Roll, action.Target, action.Result, missed, changedReason);

    internal static SpotlightAction FromRoll(RollRecord roll)
    {
        var outcome = MoveDice.Of(roll).Outcome;
        return new SpotlightAction(
            roll.MessageID.Timestamp, roll.Move ?? PlainRoll, $"{roll.Total} · {MoveRoller.Describe(outcome)}", null, null, outcome == MoveOutcome.Miss, roll.ChangedReason);
    }

    internal static bool IsMiss(RollRecord roll) => MoveDice.Of(roll).Outcome == MoveOutcome.Miss;
}

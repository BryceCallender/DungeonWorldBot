using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Encounters;

[Scoped<ITimelineService>]
public class TimelineService : ITimelineService
{
    private readonly IFightLogService _fightLog;

    public TimelineService(IFightLogService fightLog)
    {
        _fightLog = fightLog;
    }

    public async Task<TimelinePage> GetAsync(Encounter encounter, IEnumerable<Character> party, int take, DateTimeOffset? before = null)
    {
        var log = await _fightLog.GetAsync(encounter);
        var names = party.DistinctBy(c => c.ID).ToDictionary(c => c.ID, c => c.Name);

        var fromRolls = log.Rolls.Select(r => log.Logged.TryGetValue(r.MessageID, out var action)
            ? new TimelineEntry(action.Actor, SpotlightService.FromLog(action, r.MessageID.Timestamp, SpotlightService.IsMiss(r)))
            : new TimelineEntry(names.GetValueOrDefault(r.UserID, "Someone"), SpotlightService.FromRoll(r)));
        var fromLog = log.Actions
            .Where(a => a.MessageID is not { } id || !log.Rolled.Contains(id))
            .Select(a => new TimelineEntry(a.Actor, SpotlightService.FromLog(a, a.At, missed: false)));

        var all = fromRolls.Concat(fromLog).OrderByDescending(e => e.Action.At).ToList();
        var older = all.Where(e => before is not { } b || e.Action.At < b).ToList();
        return new TimelinePage(older.Take(take).ToList(), all.Count, older.Count > take);
    }
}

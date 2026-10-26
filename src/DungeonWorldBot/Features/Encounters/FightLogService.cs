using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Encounters;

[Scoped<IFightLogService>]
public class FightLogService : IFightLogService
{
    private readonly DungeonWorldContext _context;
    private readonly IRollStatsService _rollStats;
    private readonly IEncounterService _encounterService;

    public FightLogService(DungeonWorldContext context, IRollStatsService rollStats, IEncounterService encounterService)
    {
        _context = context;
        _rollStats = rollStats;
        _encounterService = encounterService;
    }

    public async Task<FightLog> GetAsync(Encounter encounter)
    {
        var actions = await _encounterService.GetActionsAsync(encounter);
        var rolls = _rollStats.GameChatChannelID is { } gameChat
            ? (await _context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync())
                .Where(r => r.MessageID.Timestamp >= encounter.StartedAt)
                .OrderBy(r => r.MessageID.Value)
                .ToList()
            : [];

        var logged = actions.Where(a => a.MessageID is not null).Select(a => a.MessageID!.Value).ToHashSet();
        var loggedElsewhere = (await _context.EncounterActions.AsNoTracking()
                .Where(a => a.EncounterID != encounter.ID && a.MessageID != null)
                .Select(a => a.MessageID)
                .ToListAsync())
            .Select(id => id!.Value)
            .ToHashSet();
        var running = await _encounterService.GetRunningAsync();

        return new FightLog(
            actions,
            rolls
                .Where(r => logged.Contains(r.MessageID)
                            || (!loggedElsewhere.Contains(r.MessageID) && EncounterService.FightFor(running, r.UserID)?.ID == encounter.ID))
                .ToList());
    }
}

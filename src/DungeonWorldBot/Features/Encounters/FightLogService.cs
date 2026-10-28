using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Features.Encounters;

[Scoped<IFightLogService>]
public class FightLogService(
    DungeonWorldContext context,
    IRollStatsService rollStats,
    IEncounterService encounterService)
    : IFightLogService
{
    public async Task<FightLog> GetAsync(Encounter encounter)
    {
        var actions = await encounterService.GetActionsAsync(encounter);
        var rolls = rollStats.GameChatChannelID is { } gameChat
            ? (await context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync())
                .Where(r => r.MessageID.Timestamp >= encounter.StartedAt)
                .OrderBy(r => r.MessageID.Value)
                .ToList()
            : [];

        var logged = actions.Where(a => a.MessageID is not null).Select(a => a.MessageID!.Value).ToHashSet();
        var loggedElsewhere = (await context.EncounterActions.AsNoTracking()
                .Where(a => a.EncounterID != encounter.ID && a.MessageID != null)
                .Select(a => a.MessageID)
                .ToListAsync())
            .Select(id => id!.Value)
            .ToHashSet();
        var running = await encounterService.GetRunningAsync();

        return new FightLog(
            actions,
            rolls
                .Where(r => logged.Contains(r.MessageID)
                            || (!loggedElsewhere.Contains(r.MessageID) && EncounterService.FightFor(running, r.UserID)?.ID == encounter.ID))
                .ToList());
    }
}

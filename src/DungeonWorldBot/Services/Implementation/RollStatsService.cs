using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Moves;
using DungeonWorldBot.Stats;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Services.Implementation;

public class RollStatsService : IRollStatsService
{
    private readonly DungeonWorldContext _dungeonWorldContext;

    public RollStatsService(DungeonWorldContext dungeonWorldContext)
    {
        _dungeonWorldContext = dungeonWorldContext;
    }

    public async Task RecordAsync(Snowflake messageID, Snowflake channelID, Snowflake userID, DiceResult roll)
    {
        if (MoveDice.From(roll) is not { } dice)
            return;

        _dungeonWorldContext.Rolls.Add(new RollRecord
        {
            MessageID = messageID,
            ChannelID = channelID,
            UserID = userID,
            First = dice.First,
            Second = dice.Second,
            Total = dice.Total,
        });

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<int> AddMissingAsync(IReadOnlyList<RollRecord> rolls)
    {
        if (rolls.Count == 0)
            return 0;

        var ids = rolls.Select(r => r.MessageID).ToList();
        var existing = (await _dungeonWorldContext.Rolls
                .Where(r => ids.Contains(r.MessageID))
                .Select(r => r.MessageID)
                .ToListAsync())
            .ToHashSet();

        var missing = rolls.Where(r => !existing.Contains(r.MessageID)).ToList();
        _dungeonWorldContext.Rolls.AddRange(missing);
        await _dungeonWorldContext.SaveChangesAsync();

        return missing.Count;
    }

    public async Task<List<PlayerRollStats>> GetStatsAsync()
    {
        var rolls = await _dungeonWorldContext.Rolls.AsNoTracking().ToListAsync();

        return rolls
            .GroupBy(r => r.UserID)
            .Select(g =>
            {
                var dice = g.Select(r => new MoveDice(r.First, r.Second, r.Total)).ToList();
                return new PlayerRollStats(
                    g.Key,
                    dice.Count,
                    dice.Count(d => d.Outcome == MoveOutcome.StrongHit),
                    dice.Count(d => d.Outcome == MoveOutcome.WeakHit),
                    dice.Count(d => d.Outcome == MoveOutcome.Miss),
                    dice.Count(d => d.IsNatural12),
                    dice.Count(d => d.IsNatural2));
            })
            .OrderByDescending(s => s.Rolls)
            .ToList();
    }
}

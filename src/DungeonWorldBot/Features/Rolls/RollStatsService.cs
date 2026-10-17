using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Infrastructure;
using d20Tek.DiceNotation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

[Scoped<IRollStatsService>]
public class RollStatsService : IRollStatsService
{
    /// <summary>
    /// The campaign's game chat, used unless the GameChatChannelId setting says otherwise.
    /// </summary>
    public const ulong DefaultGameChatChannelID = 1325143775139467325;

    private readonly DungeonWorldContext _dungeonWorldContext;

    public RollStatsService(DungeonWorldContext dungeonWorldContext, IConfiguration? configuration = null)
    {
        _dungeonWorldContext = dungeonWorldContext;

        // Without configuration (as in tests) there's no game chat, so nothing counts
        if (configuration is not null)
            GameChatChannelID = new Snowflake(configuration.GetValue<ulong?>("GameChatChannelId") ?? DefaultGameChatChannelID);
    }

    public Snowflake? GameChatChannelID { get; }

    public async Task RecordAsync(Snowflake messageID, Snowflake channelID, Snowflake userID, DiceResult roll, RollLabel? label = null)
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
            Move = label?.Move,
            Stat = label?.Stat,
        });

        await _dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<int> AddMissingAsync(IReadOnlyList<RollRecord> rolls)
    {
        if (rolls.Count == 0)
            return 0;

        var ids = rolls.Select(r => r.MessageID).ToList();
        var existing = await _dungeonWorldContext.Rolls
            .Where(r => ids.Contains(r.MessageID))
            .ToDictionaryAsync(r => r.MessageID);

        // Rolls recorded before moves and stats were kept get them from the scan
        foreach (var roll in rolls)
        {
            if (!existing.TryGetValue(roll.MessageID, out var recorded))
                continue;

            recorded.Move ??= roll.Move;
            recorded.Stat ??= roll.Stat;
        }

        var missing = rolls.Where(r => !existing.ContainsKey(r.MessageID)).ToList();
        _dungeonWorldContext.Rolls.AddRange(missing);
        await _dungeonWorldContext.SaveChangesAsync();

        return missing.Count;
    }

    public async Task<List<PlayerRollStats>> GetStatsAsync()
    {
        // Every roll is recorded with its channel, but only game chat rolls count
        if (GameChatChannelID is not { } gameChat)
            return [];

        var rolls = await _dungeonWorldContext.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync();

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

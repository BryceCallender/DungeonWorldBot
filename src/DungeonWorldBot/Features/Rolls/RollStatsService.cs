using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Infrastructure;
using d20Tek.DiceNotation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

[Scoped<IRollStatsService>]
public class RollStatsService(DungeonWorldContext dungeonWorldContext, IOptions<BotSettings>? settings = null) : IRollStatsService
{
    /// <summary>
    /// The campaign's game chat, used unless <see cref="BotSettings.GameChatChannelID"/> says otherwise.
    /// </summary>
    public const ulong DefaultGameChatChannelID = 1325143775139467325;

    // Without settings (as in tests) there's no game chat, so nothing counts
    public Snowflake? GameChatChannelID { get; } = settings is null ? null : new Snowflake(settings.Value.GameChatChannelID);

    public async Task RecordAsync(Snowflake messageID, Snowflake channelID, Snowflake userID, DiceResult roll, RollLabel? label = null)
    {
        if (MoveDice.From(roll) is not { } dice)
            return;

        dungeonWorldContext.Rolls.Add(new RollRecord
        {
            MessageID = messageID,
            ChannelID = channelID,
            UserID = userID,
            First = dice.First,
            Second = dice.Second,
            Total = dice.Total,
            Modifier = dice.Modifier,
            Modifiers = label?.Modifiers,
            Move = label?.Move,
            Stat = label?.Stat,
        });

        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<int> AddMissingAsync(IReadOnlyList<RollRecord> rolls)
    {
        if (rolls.Count == 0)
            return 0;

        var ids = rolls.Select(r => r.MessageID).ToList();
        var existing = await dungeonWorldContext.Rolls
            .Where(r => ids.Contains(r.MessageID))
            .ToDictionaryAsync(r => r.MessageID);

        // Rolls recorded before moves and stats were kept get them from the scan
        foreach (var roll in rolls)
        {
            if (!existing.TryGetValue(roll.MessageID, out var recorded))
                continue;

            recorded.Move ??= roll.Move;
            recorded.Stat ??= roll.Stat;
            recorded.Modifiers ??= roll.Modifiers;
        }

        var missing = rolls.Where(r => !existing.ContainsKey(r.MessageID)).ToList();
        dungeonWorldContext.Rolls.AddRange(missing);
        await dungeonWorldContext.SaveChangesAsync();

        return missing.Count;
    }

    public async Task ChangeOutcomeAsync(Snowflake messageID, MoveOutcome? outcome, string reason, Snowflake changedBy)
    {
        if (await dungeonWorldContext.Rolls.FindAsync(messageID) is not { } roll)
            return;

        roll.ChangedOutcome = outcome;
        roll.ChangedReason = outcome is null ? null : reason;
        roll.ChangedBy = outcome is null ? null : changedBy;
        await dungeonWorldContext.SaveChangesAsync();
    }

    public async Task<List<PlayerRollStats>> GetStatsAsync()
    {
        // Every roll is recorded with its channel, but only game chat rolls count
        if (GameChatChannelID is not { } gameChat)
            return [];

        var rolls = await dungeonWorldContext.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync();

        return rolls
            .GroupBy(r => r.UserID)
            .Select(g =>
            {
                var dice = g.Select(MoveDice.Of).ToList();
                return new PlayerRollStats(
                    g.Key,
                    dice.Count,
                    dice.Count(d => d.Outcome == MoveOutcome.StrongHit),
                    dice.Count(d => d.Outcome == MoveOutcome.WeakHit),
                    dice.Count(d => d.Outcome == MoveOutcome.Miss),
                    dice.Count(d => d.IsNatural12),
                    dice.Count(d => d.IsNatural2),
                    dice.Count(d => d.LiftedByModifier),
                    dice.Count(d => d.SunkByModifier));
            })
            .OrderByDescending(s => s.Rolls)
            .ToList();
    }
}

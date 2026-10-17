using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Party;

/// <param name="Sums">How many times each natural 2d6 sum came up, indexed 0–10 for sums 2–12.</param>
public sealed record DiceSpread(int Rolls, double AverageNatural, double AverageTotal, IReadOnlyList<int> Sums)
{
    public static DiceSpread Of(IReadOnlyCollection<MoveDice> dice)
    {
        var sums = new int[11];
        foreach (var d in dice)
            sums[d.First + d.Second - 2]++;

        return new DiceSpread(
            dice.Count,
            dice.Count == 0 ? 0 : dice.Average(d => d.First + d.Second),
            dice.Count == 0 ? 0 : dice.Average(d => d.Total),
            sums);
    }

    /// <summary>
    /// The share of fair 2d6 rolls that land on each sum from 2 to 12.
    /// </summary>
    public static double Expected(int sum) => (6 - Math.Abs(sum - 7)) / 36.0;
}

/// <summary>
/// How a group of rolls went, like every Hack and Slash or every +DEX roll.
/// </summary>
public sealed record OutcomeTally(string Name, int Rolls, int StrongHits, int WeakHits, int Misses)
{
    public static OutcomeTally Of(string name, IReadOnlyCollection<MoveDice> dice) => new(
        name,
        dice.Count,
        dice.Count(d => d.Outcome == MoveOutcome.StrongHit),
        dice.Count(d => d.Outcome == MoveOutcome.WeakHit),
        dice.Count(d => d.Outcome == MoveOutcome.Miss));
}

/// <param name="Players">How many people other than the DM rolled.</param>
/// <param name="Misses">Each miss marks XP.</param>
public sealed record SessionRolls(DateTime Started, int Players, int Rolls, int Hits, int Misses, int Natural12s, int Natural2s, double AverageNatural);

/// <param name="Unlabelled">Rolls that weren't for a known move, like plain dice or ".dex dodge the trap".</param>
public sealed record RollBreakdown(
    IReadOnlyList<OutcomeTally> Moves,
    IReadOnlyList<OutcomeTally> Stats,
    IReadOnlyList<SessionRolls> Sessions,
    int Unlabelled);

/// <summary>
/// Reads the raw game chat rolls, for the dice spreads the bot's own roll stats don't work out.
/// </summary>
public class RollReader
{
    private readonly DungeonWorldContext _context;
    private readonly IRollStatsService _rollStats;

    public RollReader(DungeonWorldContext context, IRollStatsService rollStats)
    {
        _context = context;
        _rollStats = rollStats;
    }

    /// <param name="leaveOut">Someone whose rolls count only on their own, like the DM.</param>
    /// <returns>Everyone else's spread together, then each person's by Discord ID.</returns>
    public async Task<(DiceSpread Table, IReadOnlyDictionary<ulong, DiceSpread> Players)> GetSpreadsAsync(ulong? leaveOut = null)
    {
        // Same rule as the bot's stats: only game chat rolls count
        if (_rollStats.GameChatChannelID is not { } gameChat)
            return (DiceSpread.Of([]), new Dictionary<ulong, DiceSpread>());

        var rolls = await GameChatRollsAsync(gameChat);

        return (
            DiceSpread.Of(rolls.Where(r => r.UserID.Value != leaveOut).Select(r => new MoveDice(r.First, r.Second, r.Total)).ToList()),
            rolls
                .GroupBy(r => r.UserID.Value)
                .ToDictionary(g => g.Key, g => DiceSpread.Of(g.Select(r => new MoveDice(r.First, r.Second, r.Total)).ToList())));
    }

    /// <summary>
    /// A break in rolling this long means the next roll is a new session, so a game that runs past midnight is one session.
    /// </summary>
    public static readonly TimeSpan SessionGap = TimeSpan.FromHours(8);

    /// <param name="leaveOut">Someone whose rolls aren't counted, like the DM.</param>
    public async Task<RollBreakdown> GetBreakdownAsync(ulong? leaveOut = null)
    {
        if (_rollStats.GameChatChannelID is not { } gameChat)
            return new RollBreakdown([], [], [], 0);

        var rolls = (await GameChatRollsAsync(gameChat)).Where(r => r.UserID.Value != leaveOut).ToList();

        var moves = rolls
            .Where(r => r.Move is not null)
            .GroupBy(r => r.Move!)
            .Select(g => OutcomeTally.Of(g.Key, Dice(g)))
            .OrderByDescending(t => t.Rolls)
            .ToList();

        var stats = rolls
            .Where(r => r.Stat is not null)
            .GroupBy(r => r.Stat!.Value)
            .OrderBy(g => g.Key)
            .Select(g => OutcomeTally.Of($"+{g.Key.ToString().ToUpper()}", Dice(g)))
            .ToList();

        return new RollBreakdown(moves, stats, Sessions(rolls), rolls.Count(r => r.Move is null));
    }

    // Newest first
    private static List<SessionRolls> Sessions(IEnumerable<RollRecord> rolls)
    {
        var sessions = new List<List<RollRecord>>();
        foreach (var roll in rolls.OrderBy(r => r.MessageID.Value))
        {
            if (sessions.Count == 0 || roll.MessageID.Timestamp - sessions[^1][^1].MessageID.Timestamp > SessionGap)
                sessions.Add([]);

            sessions[^1].Add(roll);
        }

        return sessions
            .Select(s =>
            {
                var dice = Dice(s);
                return new SessionRolls(
                    s[0].MessageID.Timestamp.LocalDateTime,
                    s.Select(r => r.UserID).Distinct().Count(),
                    dice.Count,
                    dice.Count(d => d.Outcome != MoveOutcome.Miss),
                    dice.Count(d => d.Outcome == MoveOutcome.Miss),
                    dice.Count(d => d.IsNatural12),
                    dice.Count(d => d.IsNatural2),
                    dice.Average(d => d.First + d.Second));
            })
            .Reverse()
            .ToList();
    }

    private static List<MoveDice> Dice(IEnumerable<RollRecord> rolls) => rolls.Select(r => new MoveDice(r.First, r.Second, r.Total)).ToList();

    private Task<List<RollRecord>> GameChatRollsAsync(Snowflake gameChat) =>
        _context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync();
}

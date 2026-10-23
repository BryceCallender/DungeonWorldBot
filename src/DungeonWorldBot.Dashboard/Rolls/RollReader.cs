using DungeonWorldBot.Dashboard.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Rolls;

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

    /// <summary>
    /// Who in the party has rolled since a fight started, and who's been left out. Only the characters' own rolls
    /// count, so the DM's don't.
    /// </summary>
    public async Task<Spotlight> GetSpotlightAsync(DateTimeOffset since, IEnumerable<CharacterView> party)
    {
        var living = party.Where(c => c.Character.Status != Status.Dead).ToList();
        var rolls = _rollStats.GameChatChannelID is { } gameChat
            ? (await GameChatRollsAsync(gameChat)).Where(r => r.MessageID.Timestamp >= since).OrderBy(r => r.MessageID.Value).ToList()
            : [];

        var byPlayer = living.ToDictionary(c => c, c => rolls.Where(r => r.UserID == c.Character.ID).ToList());
        var partyRolls = byPlayer.Values.SelectMany(r => r).ToList();
        DateTimeOffset? Last(CharacterView c) => byPlayer[c] is [.., var last] ? last.MessageID.Timestamp : null;

        var lines = living
            .Select(c =>
            {
                var own = byPlayer[c];
                var last = Last(c);
                var others = living.Where(o => o != c).ToList();
                // Before anyone rolls, nobody's been left out yet
                var quiet = last is null
                    ? partyRolls.Count > 0
                    : others.Count > 0 && others.All(o => Last(o) > last);

                return new SpotlightLine(c, own.Count, own.Count(IsMiss), last, own.LastOrDefault()?.Move, quiet);
            })
            .OrderBy(l => l.LastRolled ?? DateTimeOffset.MinValue)
            .ThenBy(l => l.Character.Name)
            .ToList();

        return new Spotlight(lines, partyRolls.Count, partyRolls.Count(IsMiss));
    }

    private static bool IsMiss(RollRecord roll) => new MoveDice(roll.First, roll.Second, roll.Total).Outcome == MoveOutcome.Miss;

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

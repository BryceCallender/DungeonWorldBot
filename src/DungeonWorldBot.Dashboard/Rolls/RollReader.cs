using DungeonWorldBot.Dashboard.Encounters;
using DungeonWorldBot.Dashboard.Party;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Sessions;
using Microsoft.Data.Sqlite;
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

    /// <inheritdoc cref="SessionService.Gap"/>
    public static readonly TimeSpan SessionGap = SessionService.Gap;

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

        return new RollBreakdown(moves, stats, Sessions(rolls, await RecordedSessionsAsync()), rolls.Count(r => r.Move is null));
    }

    // Enough rolls back to find everyone's streak, without reading the whole history
    private const int StreakRolls = 400;

    /// <summary>
    /// The streak each player is on, by Discord ID; only those on one.
    /// </summary>
    public async Task<Dictionary<ulong, RollStreak>> GetStreaksAsync()
    {
        if (_rollStats.GameChatChannelID is not { } gameChat)
            return new Dictionary<ulong, RollStreak>();

        // EF won't order by IDs stored as ulong in SQLite, though the column is a plain INTEGER that sorts fine
        var recent = await _context.Rolls
            .FromSql($"SELECT * FROM Rolls WHERE ChannelID = {gameChat.Value} ORDER BY MessageID DESC LIMIT {StreakRolls}")
            .AsNoTracking()
            .ToListAsync();

        return recent
            .GroupBy(r => r.UserID.Value)
            .Select(g => (Player: g.Key, Streak: RollStreak.Of(g)))
            .Where(p => p.Streak is not null)
            .ToDictionary(p => p.Player, p => p.Streak!);
    }

    /// <summary>
    /// How many sessions back each player's luck goes.
    /// </summary>
    public const int LuckSessions = 12;

    // The heatmap's rows; beyond these the moves are rarely rolled enough to say much
    private const int HeatmapMoves = 10;

    // Fewer rolls than this in a night is too few to call anyone's dice hot or cold
    private const int AwardRolls = 3;

    /// <param name="leaveOut">Someone whose rolls aren't counted, like the DM.</param>
    public async Task<RollNerdery> GetNerderyAsync(ulong? leaveOut = null)
    {
        if (_rollStats.GameChatChannelID is not { } gameChat)
            return new RollNerdery([], new MoveHeatmap([], []), null);

        var rolls = (await GameChatRollsAsync(gameChat)).Where(r => r.UserID.Value != leaveOut).ToList();
        var sessions = SessionGroups(rolls, await RecordedSessionsAsync());

        var recent = sessions.TakeLast(LuckSessions).ToList();
        var luck = rolls
            .GroupBy(r => r.UserID.Value)
            .OrderByDescending(g => g.Count())
            .Select(g => new LuckTrend(g.Key, recent
                .Select(s => s.Rolls.Where(r => r.UserID.Value == g.Key).ToList())
                .Select(mine => mine.Count == 0 ? (double?)null : mine.Average(r => r.First + r.Second))
                .ToList()))
            .ToList();

        var labelled = rolls.Where(r => r.Move is not null).ToList();
        var players = labelled.GroupBy(r => r.UserID.Value).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
        var heatmap = new MoveHeatmap(players, labelled
            .GroupBy(r => r.Move!)
            .OrderByDescending(g => g.Count())
            .Take(HeatmapMoves)
            .Select(g => new HeatmapRow(g.Key, g.Count(), players
                .Select(p => g.Where(r => r.UserID.Value == p).ToList() is { Count: > 0 } theirs ? OutcomeTally.Of(g.Key, Dice(theirs)) : null)
                .ToList()))
            .ToList());

        return new RollNerdery(luck, heatmap, sessions.Count == 0 ? null : await AwardsAsync(sessions[^1]));
    }

    /// <summary>
    /// The game chat's rolls between two moments, oldest first.
    /// </summary>
    public async Task<List<RollRecord>> GetRollsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        if (_rollStats.GameChatChannelID is not { } gameChat)
            return [];

        return (await GameChatRollsAsync(gameChat))
            .Where(r => r.MessageID.Timestamp >= from && r.MessageID.Timestamp <= to)
            .OrderBy(r => r.MessageID.Value)
            .ToList();
    }

    /// <summary>
    /// Who stood out between two moments, like a session.
    /// </summary>
    /// <param name="leaveOut">Someone who can't win, like the DM.</param>
    public async Task<SessionAwards?> GetAwardsAsync(DateTimeOffset from, DateTimeOffset to, ulong? leaveOut = null)
    {
        var rolls = (await GetRollsAsync(from, to)).Where(r => r.UserID.Value != leaveOut).ToList();
        return rolls.Count == 0 ? null : await AwardsAsync(new RollSession(rolls, null));
    }

    private async Task<SessionAwards> AwardsAsync(RollSession night)
    {
        var session = night.Rolls;
        var awards = new List<Award>();
        var byPlayer = session.GroupBy(r => r.UserID.Value).Select(g => (Player: g.Key, Dice: Dice(g))).ToList();
        var steady = byPlayer.Where(p => p.Dice.Count >= AwardRolls).ToList();

        if (steady.Count > 0)
        {
            var hot = steady.MaxBy(p => p.Dice.Average(d => d.First + d.Second));
            awards.Add(new Award("🔥", "Hot dice", hot.Player, $"averaged {hot.Dice.Average(d => d.First + d.Second):0.0} over {hot.Dice.Count} rolls"));

            var cold = steady.MinBy(p => p.Dice.Average(d => d.First + d.Second));
            if (steady.Count > 1 && cold.Player != hot.Player)
                awards.Add(new Award("🧊", "Cursed dice", cold.Player, $"averaged {cold.Dice.Average(d => d.First + d.Second):0.0} over {cold.Dice.Count} rolls"));

            var sharp = steady.MaxBy(p => p.Dice.Count(d => d.Outcome != MoveOutcome.Miss) / (double)p.Dice.Count);
            var hits = sharp.Dice.Count(d => d.Outcome != MoveOutcome.Miss);
            awards.Add(new Award("🎯", "Sharpshooter", sharp.Player, $"hit {hits} of {sharp.Dice.Count}"));
        }

        var busiest = byPlayer.MaxBy(p => p.Dice.Count);
        awards.Add(new Award("🗣️", "Busiest", busiest.Player, $"{busiest.Dice.Count} rolls"));

        var blessed = byPlayer.MaxBy(p => p.Dice.Count(d => d.IsNatural12));
        if (blessed.Dice.Count(d => d.IsNatural12) is > 0 and var twelves)
            awards.Add(new Award("✨", "Blessed", blessed.Player, $"{twelves} natural 12{(twelves == 1 ? "" : "s")}"));

        var snakes = byPlayer.MaxBy(p => p.Dice.Count(d => d.IsNatural2));
        if (snakes.Dice.Count(d => d.IsNatural2) is > 0 and var twos)
            awards.Add(new Award("🐍", "Snake charmer", snakes.Player, $"{twos} snake eyes"));

        awards.AddRange(await FightAwardsAsync(session));

        return new SessionAwards(session[0].MessageID.Timestamp.LocalDateTime, awards, night.Name);
    }

    // Who dealt and took the most damage in fights that night, from what the fights' timelines say each hit did
    private async Task<List<Award>> FightAwardsAsync(List<RollRecord> session)
    {
        var from = session[0].MessageID.Timestamp - TimeSpan.FromHours(1);
        var to = session[^1].MessageID.Timestamp + TimeSpan.FromHours(1);

        List<EncounterAction> actions;
        try
        {
            actions = (await _context.EncounterActions.AsNoTracking().Where(a => a.Result != null).ToListAsync())
                .Where(a => a.At >= from && a.At <= to)
                .ToList();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            // The bot hasn't added fights to its database yet
            return [];
        }

        int Damage(EncounterAction a) => FightDamage.Of(a.Result);

        // Value tuples, so nobody at all comes back as 0 damage rather than throwing
        var awards = new List<Award>();
        var dealt = actions
            .Where(a => a.CharacterID is not null)
            .GroupBy(a => a.CharacterID!.Value.Value)
            .Select(g => (Player: g.Key, Damage: g.Sum(Damage)))
            .OrderByDescending(p => p.Damage)
            .FirstOrDefault();
        if (dealt.Damage > 0)
            awards.Add(new Award("💥", "Heavy hitter", dealt.Player, $"dealt {dealt.Damage} damage"));

        // Enemies' attacks name who they hit
        var party = await _context.Characters.AsNoTracking().Select(c => new { c.ID, c.Name }).ToListAsync();
        var taken = actions
            .Where(a => a.EnemyID is not null && a.Target is not null)
            .Select(a => (Who: party.FirstOrDefault(c => string.Equals(c.Name, a.Target, StringComparison.OrdinalIgnoreCase)), Damage: Damage(a)))
            .Where(t => t.Who is not null && t.Damage > 0)
            .GroupBy(t => t.Who!.ID.Value)
            .Select(g => (Player: g.Key, Damage: g.Sum(t => t.Damage)))
            .OrderByDescending(p => p.Damage)
            .FirstOrDefault();
        if (taken.Damage > 0)
            awards.Add(new Award("🛡️", "Punching bag", taken.Player, $"took {taken.Damage} damage"));

        return awards;
    }

    /// <summary>
    /// A session's rolls, oldest first, with its name if it was marked with /session start.
    /// </summary>
    private record RollSession(List<RollRecord> Rolls, string? Name, int? Number = null);

    // The sessions marked with /session start, oldest first. None if the bot's database predates them.
    private async Task<List<GameSession>> RecordedSessionsAsync()
    {
        try
        {
            return await _context.Sessions.AsNoTracking().OrderBy(s => s.ID).ToListAsync();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table"))
        {
            return [];
        }
    }

    // Oldest first. Rolls during a marked session are that session; the rest are split wherever nobody rolled for
    // SessionGap, as they were before sessions could be marked.
    private static List<RollSession> SessionGroups(IEnumerable<RollRecord> rolls, List<GameSession> recorded)
    {
        var sessions = new List<RollSession>();
        var marked = recorded.Select((s, i) => (Session: s, Group: new RollSession([], s.Title is { } t ? $"Session {i + 1}: {t}" : $"Session {i + 1}", i + 1))).ToList();
        RollSession? guessed = null;

        foreach (var roll in rolls.OrderBy(r => r.MessageID.Value))
        {
            var at = roll.MessageID.Timestamp;
            if (marked.FirstOrDefault(m => at >= m.Session.StartedAt && at <= (m.Session.EndedAt ?? DateTimeOffset.MaxValue)) is { Group: { } group })
            {
                group.Rolls.Add(roll);
                guessed = null;
                continue;
            }

            if (guessed is null || at - guessed.Rolls[^1].MessageID.Timestamp > SessionGap)
                sessions.Add(guessed = new RollSession([], null));

            guessed.Rolls.Add(roll);
        }

        return sessions
            .Concat(marked.Select(m => m.Group))
            .Where(s => s.Rolls.Count > 0)
            .OrderBy(s => s.Rolls[0].MessageID.Value)
            .ToList();
    }

    // Newest first
    private static List<SessionRolls> Sessions(IEnumerable<RollRecord> rolls, List<GameSession> recorded) =>
        SessionGroups(rolls, recorded)
            .Select(night =>
            {
                var s = night.Rolls;
                var dice = Dice(s);
                return new SessionRolls(
                    s[0].MessageID.Timestamp.LocalDateTime,
                    s.Select(r => r.UserID).Distinct().Count(),
                    dice.Count,
                    dice.Count(d => d.Outcome != MoveOutcome.Miss),
                    dice.Count(d => d.Outcome == MoveOutcome.Miss),
                    dice.Count(d => d.IsNatural12),
                    dice.Count(d => d.IsNatural2),
                    dice.Average(d => d.First + d.Second),
                    night.Name,
                    night.Number);
            })
            .Reverse()
            .ToList();

    private static List<MoveDice> Dice(IEnumerable<RollRecord> rolls) => rolls.Select(r => new MoveDice(r.First, r.Second, r.Total)).ToList();

    private Task<List<RollRecord>> GameChatRollsAsync(Snowflake gameChat) =>
        _context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync();
}

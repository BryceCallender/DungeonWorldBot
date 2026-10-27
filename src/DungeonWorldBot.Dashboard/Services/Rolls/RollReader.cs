using DungeonWorldBot.Dashboard.Models.Rolls;
using DungeonWorldBot.Dashboard.Helpers;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Sessions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DungeonWorldBot.Dashboard.Services.Rolls;

/// <summary>
/// Reads the raw game chat rolls, for the dice spreads the bot's own roll stats don't work out.
/// </summary>
public class RollReader
{
    /// <inheritdoc cref="SessionService.Gap"/>
    public static readonly TimeSpan SessionGap = SessionService.Gap;

    /// <summary>
    /// How many sessions back each player's luck goes.
    /// </summary>
    public const int LuckSessions = 12;

    // Enough rolls back to find everyone's streak, without reading the whole history
    private const int StreakRolls = 400;

    // The heatmap's rows; beyond these the moves are rarely rolled enough to say much
    private const int HeatmapMoves = 10;

    // Fewer rolls than this in a night is too few to call anyone's dice hot or cold
    private const int AwardRolls = 3;

    private readonly DungeonWorldContext _context;
    private readonly IRollStatsService _rollStats;

    public RollReader(DungeonWorldContext context, IRollStatsService rollStats)
    {
        _context = context;
        _rollStats = rollStats;
    }

    /// <param name="leaveOut">Someone whose rolls count only on their own, like the DM.</param>
    public async Task<RollSpreads> GetSpreadsAsync(ulong? leaveOut = null)
    {
        if (await GameChatRollsAsync() is not { } rolls)
            return RollSpreads.None;

        var table = DiceSpread.Of(Dice(rolls.Where(r => r.UserID.Value != leaveOut)));
        var players = rolls
            .GroupBy(r => r.UserID.Value)
            .ToDictionary(g => g.Key, g => DiceSpread.Of(Dice(g)));

        return new RollSpreads(table, players);
    }

    /// <param name="leaveOut">Someone whose rolls aren't counted, like the DM.</param>
    public async Task<RollBreakdown> GetBreakdownAsync(ulong? leaveOut = null)
    {
        if (await GameChatRollsAsync(leaveOut) is not { } rolls)
            return new RollBreakdown([], [], [], 0);

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

        var sessions = SessionSummaries(rolls, await RecordedSessionsAsync());
        var unlabelled = rolls.Count(r => r.Move is null);

        return new RollBreakdown(moves, stats, sessions, unlabelled);
    }

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

        var streaks = new Dictionary<ulong, RollStreak>();
        foreach (var player in recent.GroupBy(r => r.UserID.Value))
        {
            if (RollStreak.Of(player) is { } streak)
                streaks[player.Key] = streak;
        }

        return streaks;
    }

    /// <param name="leaveOut">Someone whose rolls aren't counted, like the DM.</param>
    public async Task<RollNerdery> GetNerderyAsync(ulong? leaveOut = null)
    {
        if (await GameChatRollsAsync(leaveOut) is not { } rolls)
            return new RollNerdery([], new MoveHeatmap([], []), null);

        var sessions = SessionGroups(rolls, await RecordedSessionsAsync());
        var luck = LuckTrends(rolls, sessions.TakeLast(LuckSessions).ToList());
        var latestAwards = sessions.Count == 0 ? null : await AwardsAsync(sessions[^1]);

        return new RollNerdery(luck, Heatmap(rolls), latestAwards);
    }

    /// <summary>
    /// The game chat's rolls between two moments, oldest first.
    /// </summary>
    public async Task<List<RollRecord>> GetRollsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        if (await GameChatRollsAsync() is not { } rolls)
            return [];

        return rolls
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

    // Each player's average dice in each of the recent sessions (null where they didn't roll), busiest rollers first
    private static List<LuckTrend> LuckTrends(List<RollRecord> rolls, List<RollSession> recent) =>
        rolls
            .GroupBy(r => r.UserID.Value)
            .OrderByDescending(g => g.Count())
            .Select(player => new LuckTrend(
                player.Key,
                recent.Select(s => AverageNatural(s.Rolls.Where(r => r.UserID.Value == player.Key))).ToList()))
            .ToList();

    private static double? AverageNatural(IEnumerable<RollRecord> rolls)
    {
        var sums = rolls.Select(r => r.First + r.Second).ToList();
        return sums.Count == 0 ? null : sums.Average();
    }

    // The most rolled moves against who rolled them, busiest rollers first
    private static MoveHeatmap Heatmap(List<RollRecord> rolls)
    {
        var labelled = rolls.Where(r => r.Move is not null).ToList();
        var players = labelled
            .GroupBy(r => r.UserID.Value)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

        var rows = labelled
            .GroupBy(r => r.Move!)
            .OrderByDescending(g => g.Count())
            .Take(HeatmapMoves)
            .Select(move => new HeatmapRow(move.Key, move.Count(), players.Select(p => TallyFor(move, p)).ToList()))
            .ToList();

        return new MoveHeatmap(players, rows);
    }

    // How a player did with a move, or null if they never rolled it
    private static OutcomeTally? TallyFor(IGrouping<string, RollRecord> move, ulong player)
    {
        var theirs = move.Where(r => r.UserID.Value == player).ToList();
        return theirs.Count == 0 ? null : OutcomeTally.Of(move.Key, Dice(theirs));
    }

    private async Task<SessionAwards> AwardsAsync(RollSession session)
    {
        var rolls = session.Rolls;
        var byPlayer = rolls
            .GroupBy(r => r.UserID.Value)
            .Select(g => new PlayerDice(g.Key, Dice(g)))
            .ToList();

        var awards = DiceAwards(byPlayer);
        awards.AddRange(await FightAwardsAsync(rolls));

        return new SessionAwards(rolls[0].MessageID.Timestamp.LocalDateTime, awards, session.Name);
    }

    // byPlayer is never empty, since a session has at least one roll
    private static List<Award> DiceAwards(List<PlayerDice> byPlayer)
    {
        var awards = new List<Award>();

        var steady = byPlayer.Where(p => p.Dice.Count >= AwardRolls).ToList();
        if (steady.Count > 0)
        {
            var hot = steady.MaxBy(p => p.AverageNatural)!;
            awards.Add(new Award("🔥", "Hot dice", hot.Player, $"averaged {hot.AverageNatural:0.0} over {hot.Dice.Count} rolls"));

            var cold = steady.MinBy(p => p.AverageNatural)!;
            if (cold.Player != hot.Player)
                awards.Add(new Award("🧊", "Cursed dice", cold.Player, $"averaged {cold.AverageNatural:0.0} over {cold.Dice.Count} rolls"));

            var sharp = steady.MaxBy(p => p.Hits / (double)p.Dice.Count)!;
            awards.Add(new Award("🎯", "Sharpshooter", sharp.Player, $"hit {sharp.Hits} of {sharp.Dice.Count}"));
        }

        var busiest = byPlayer.MaxBy(p => p.Dice.Count)!;
        awards.Add(new Award("🗣️", "Busiest", busiest.Player, $"{busiest.Dice.Count} rolls"));

        var blessed = byPlayer.MaxBy(p => p.Natural12s)!;
        if (blessed.Natural12s > 0)
            awards.Add(new Award("✨", "Blessed", blessed.Player, Format.Plural(blessed.Natural12s, "natural 12")));

        var snakes = byPlayer.MaxBy(p => p.Natural2s)!;
        if (snakes.Natural2s > 0)
            awards.Add(new Award("🐍", "Snake charmer", snakes.Player, $"{snakes.Natural2s} snake eyes"));

        return awards;
    }

    // Who dealt and took the most damage in fights that night, from what the fights' timelines say each hit did
    private async Task<List<Award>> FightAwardsAsync(List<RollRecord> session)
    {
        var actions = await FightActionsAsync(
            session[0].MessageID.Timestamp - TimeSpan.FromHours(1),
            session[^1].MessageID.Timestamp + TimeSpan.FromHours(1));

        int Damage(EncounterAction a) => FightDamage.Of(a.Result);

        var awards = new List<Award>();

        var dealt = actions
            .Where(a => a.CharacterID is not null)
            .GroupBy(a => a.CharacterID!.Value.Value)
            .Select(g => new PlayerDamage(g.Key, g.Sum(Damage)))
            .MaxBy(p => p.Damage);
        if (dealt is { Damage: > 0 })
            awards.Add(new Award("💥", "Heavy hitter", dealt.Player, $"dealt {dealt.Damage} damage"));

        // Enemies' attacks name who they hit
        var party = await _context.Characters.AsNoTracking().Select(c => new { c.ID, c.Name }).ToListAsync();
        ulong? CharacterNamed(string name) =>
            party.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) is { } c ? c.ID.Value : null;

        var taken = actions
            .Where(a => a.EnemyID is not null && a.Target is not null && Damage(a) > 0)
            .GroupBy(a => CharacterNamed(a.Target!))
            .Where(g => g.Key is not null)
            .Select(g => new PlayerDamage(g.Key!.Value, g.Sum(Damage)))
            .MaxBy(p => p.Damage);
        if (taken is { Damage: > 0 })
            awards.Add(new Award("🛡️", "Punching bag", taken.Player, $"took {taken.Damage} damage"));

        return awards;
    }

    // Fight actions with a result between two moments. None if the bot hasn't added fights to its database yet.
    private async Task<List<EncounterAction>> FightActionsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        try
        {
            // SQLite can't compare times, so they're filtered here
            return (await _context.EncounterActions.AsNoTracking().Where(a => a.Result != null).ToListAsync())
                .Where(a => a.At >= from && a.At <= to)
                .ToList();
        }
        catch (SqliteException e) when (e.Message.Contains("no such table") || e.Message.Contains("no such column"))
        {
            return [];
        }
    }

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
        var marked = recorded.Select((session, i) => MarkedSession.Of(session, i + 1)).ToList();
        var guessed = new List<RollSession>();
        RollSession? current = null;

        foreach (var roll in rolls.OrderBy(r => r.MessageID.Value))
        {
            var at = roll.MessageID.Timestamp;
            if (marked.FirstOrDefault(m => m.Covers(at)) is { } during)
            {
                during.Group.Rolls.Add(roll);
                current = null;
                continue;
            }

            if (current is null || at - current.Rolls[^1].MessageID.Timestamp > SessionGap)
                guessed.Add(current = new RollSession([], null));

            current.Rolls.Add(roll);
        }

        return guessed
            .Concat(marked.Select(m => m.Group))
            .Where(s => s.Rolls.Count > 0)
            .OrderBy(s => s.Rolls[0].MessageID.Value)
            .ToList();
    }

    // Newest first
    private static List<SessionRolls> SessionSummaries(IEnumerable<RollRecord> rolls, List<GameSession> recorded) =>
        SessionGroups(rolls, recorded)
            .Select(session =>
            {
                var dice = Dice(session.Rolls);
                return new SessionRolls(
                    session.Rolls[0].MessageID.Timestamp.LocalDateTime,
                    session.Rolls.Select(r => r.UserID).Distinct().Count(),
                    dice.Count,
                    dice.Count(d => d.Outcome != MoveOutcome.Miss),
                    dice.Count(d => d.Outcome == MoveOutcome.Miss),
                    dice.Count(d => d.IsNatural12),
                    dice.Count(d => d.IsNatural2),
                    dice.Average(d => d.First + d.Second),
                    session.Name,
                    session.Number);
            })
            .Reverse()
            .ToList();

    private static List<MoveDice> Dice(IEnumerable<RollRecord> rolls) => rolls.Select(r => MoveDice.Of(r)).ToList();

    // The game chat's rolls, besides leaveOut's. Null when there's no game chat: like the bot's stats, only its rolls count.
    private async Task<List<RollRecord>?> GameChatRollsAsync(ulong? leaveOut = null)
    {
        if (_rollStats.GameChatChannelID is not { } gameChat)
            return null;

        var rolls = await _context.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync();
        return rolls.Where(r => r.UserID.Value != leaveOut).ToList();
    }

    /// <summary>
    /// A session's rolls, oldest first, with its name if it was marked with /session start.
    /// </summary>
    private record RollSession(List<RollRecord> Rolls, string? Name, int? Number = null);

    /// <summary>
    /// A session marked with /session start, and the rolls made during it.
    /// </summary>
    private record MarkedSession(GameSession Session, RollSession Group)
    {
        public static MarkedSession Of(GameSession session, int number)
        {
            var name = session.Title is { } title ? $"Session {number}: {title}" : $"Session {number}";
            return new MarkedSession(session, new RollSession([], name, number));
        }

        public bool Covers(DateTimeOffset at) => at >= Session.StartedAt && at <= (Session.EndedAt ?? DateTimeOffset.MaxValue);
    }

    private record PlayerDice(ulong Player, List<MoveDice> Dice)
    {
        public double AverageNatural => Dice.Average(d => d.First + d.Second);
        public int Hits => Dice.Count(d => d.Outcome != MoveOutcome.Miss);
        public int Natural12s => Dice.Count(d => d.IsNatural12);
        public int Natural2s => Dice.Count(d => d.IsNatural2);
    }

    private record PlayerDamage(ulong Player, int Damage);
}

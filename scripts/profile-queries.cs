#!/usr/bin/env dotnet
// Times the queries the bot and dashboard lean on most, and saves the SQL EF Core sends for each, so two EF Core
// versions (or two versions of a query) can be compared on real data.
//
// It never touches the database it's given: that's copied into the output folder, the copy is migrated to the latest
// schema, and the copy is what's queried.
//
//     dotnet run scripts/profile-queries.cs -- profile src/DungeonWorldBot/SanaSolem.db profiles/after
//     dotnet run scripts/profile-queries.cs -- compare profiles/before profiles/after
//
// profile writes results.json (timings) and sql/<query>.sql (what was sent) to the output folder. compare prints each
// query's median time side by side, and which queries' SQL changed.

#:project ../src/DungeonWorldBot.Data/DungeonWorldBot.Data.csproj
// EF builds its model by reflection, which file-based apps turn off by default
#:property PublishAot=false

using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProfileQueries;
using Remora.Rest.Core;

const int WarmUps = 5;
const int Runs = 50;

var json = new JsonSerializerOptions { WriteIndented = true };

switch (args)
{
    case ["profile", var database, var output]:
        await ProfileAsync(Path.GetFullPath(database), Path.GetFullPath(output));
        break;
    case ["compare", var before, var after]:
        Compare(Path.GetFullPath(before), Path.GetFullPath(after));
        break;
    default:
        Console.Error.WriteLine("Usage: profile <database> <output folder> | compare <before folder> <after folder>");
        return 1;
}
return 0;

async Task ProfileAsync(string database, string output)
{
    var sqlFolder = Path.Combine(output, "sql");
    Directory.CreateDirectory(sqlFolder);
    var copy = Path.Combine(output, "profiled.db");
    CopyDatabase(database, copy);

    var recorder = new SqlRecorder();
    var options = new DbContextOptionsBuilder<DungeonWorldContext>()
        .UseSqlite($"Data Source={copy}")
        .AddInterceptors(recorder)
        .Options;

    await using (var setup = new DungeonWorldContext(options))
        await setup.Database.MigrateAsync();

    var picks = await PickAsync(options);
    var efVersion = typeof(DbContext).Assembly.GetName().Version!.ToString();
    Console.WriteLine($"EF Core {efVersion} on .NET {Environment.Version}, {Runs} runs each after {WarmUps} warm-ups");
    Console.WriteLine($"{"Query",-26} {"Median ms",10} {"p95 ms",8} {"Min ms",8} {"SQL",4}");

    List<QueryTiming> timings = [];
    foreach (var scenario in Scenarios(picks))
    {
        var timing = await TimeAsync(scenario, options, recorder);
        await File.WriteAllTextAsync(Path.Combine(sqlFolder, $"{scenario.Name}.sql"), string.Join("\n\n;\n\n", recorder.Commands) + "\n");
        timings.Add(timing);
        Console.WriteLine($"{timing.Name,-26} {timing.MedianMs,10:F3} {timing.P95Ms,8:F3} {timing.MinMs,8:F3} {timing.Commands,4}");
    }

    var profile = new Profile(efVersion, Environment.Version.ToString(), Runs, timings);
    await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(profile, json));
}

// Through SQLite's backup, so anything still in the write-ahead log comes along, and the original is only read
static void CopyDatabase(string from, string to)
{
    SqliteConnection.ClearAllPools();
    File.Delete(to);
    using var source = new SqliteConnection($"Data Source={from};Mode=ReadOnly;Pooling=False");
    using var target = new SqliteConnection($"Data Source={to};Pooling=False");
    source.Open();
    target.Open();
    source.BackupDatabase(target);
}

// The character, fight, session and channel to ask about: whichever has the most to load
static async Task<Picks> PickAsync(DbContextOptions<DungeonWorldContext> options)
{
    await using var db = new DungeonWorldContext(options);
    var character = await db.Characters.OrderByDescending(c => c.Abilities.Count + c.Stats.Count + c.Bonds.Count).Select(c => c.ID).FirstAsync();
    var fight = await db.Encounters.OrderByDescending(e => e.Actions.Count).Select(e => (int?)e.ID).FirstOrDefaultAsync();
    var session = await db.Sessions.OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync();
    var channels = await db.Rolls.Select(r => r.ChannelID).ToListAsync();
    var gameChat = channels.GroupBy(c => c).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
    return new Picks(character, fight, session, gameChat);
}

// Copies of the services' queries (CharacterService, EncounterService, SessionService, RollStatsService and the
// dashboard's readers), since those projects only build on the newest .NET
static IEnumerable<Scenario> Scenarios(Picks picks)
{
    var id = picks.Character;
    yield return new("character-sheet", db => Details(db).FirstOrDefaultAsync(c => c.ID == id));
    yield return new("character-sheet-readonly", db => Details(db).AsNoTracking().FirstOrDefaultAsync(c => c.ID == id));
    yield return new("character-journal", db => Details(db).AsNoTracking().Include(c => c.Goals).Include(c => c.Notes).FirstOrDefaultAsync(c => c.ID == id));
    yield return new("character-moves", db => db.Characters.AsNoTracking().Include(c => c.Class).Include(c => c.Abilities).Include(c => c.Transformations).FirstOrDefaultAsync(c => c.ID == id));
    yield return new("all-characters", db => Details(db).ToListAsync());
    yield return new("character-names", db => db.Characters.Select(c => new { c.ID, c.Name, c.Class.Type, c.ControlledByID }).ToListAsync());
    yield return new("character-abilities", db => db.Characters.AsNoTracking().Include(c => c.Abilities).ToListAsync());

    yield return new("running-fights", db => db.Encounters.AsSplitQuery().Include(e => e.Enemies).Include(e => e.Participants)
        .Where(e => e.EndedAt == null).OrderBy(e => e.ID).ToListAsync());
    yield return new("unannounced-fights", db => db.Encounters.Include(e => e.Enemies)
        .Where(e => e.AnnouncedAt == null || (e.EndedAt != null && e.EndAnnouncedAt == null)).OrderBy(e => e.ID).ToListAsync());
    if (picks.Fight is { } fight)
        yield return new("fight-replay", db => db.Encounters.AsNoTracking().Include(e => e.Enemies).Include(e => e.Actions).FirstOrDefaultAsync(e => e.ID == fight));

    var gameChat = picks.GameChat;
    yield return new("game-chat-rolls", db => db.Rolls.AsNoTracking().Where(r => r.ChannelID == gameChat).ToListAsync());

    yield return new("current-session", db => db.Sessions.Where(s => s.EndedAt == null).OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync());
    yield return new("all-sessions", db => db.Sessions.OrderBy(s => s.StartedAt).ToListAsync());
    if (picks.Session is { } session)
    {
        yield return new("previous-session", db => db.Sessions.Where(s => s.StartedAt < session.StartedAt).OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync());
        yield return new("session-number", db => db.Sessions.CountAsync(s => s.StartedAt <= session.StartedAt));
    }
}

// CharacterService's full sheet
static IQueryable<Character> Details(DungeonWorldContext db) =>
    db.Characters
        .AsSplitQuery()
        .Include(c => c.Class)
        .Include(c => c.Health)
        .Include(c => c.Stats)
        .Include(c => c.Bonds)
        .Include(c => c.Debilities)
        .Include(c => c.Abilities)
        .Include(c => c.Resources)
        .Include(c => c.ArmorBonuses)
        .Include(c => c.Transformations)
        .Include(c => c.Techniques)
        .Include(c => c.Inventory)
        .ThenInclude(i => i.Items);

// Each run in a context of its own, as each command and page request gets
static async Task<QueryTiming> TimeAsync(Scenario scenario, DbContextOptions<DungeonWorldContext> options, SqlRecorder recorder)
{
    List<double> times = [];
    for (var run = 0; run < WarmUps + Runs; run++)
    {
        recorder.Commands.Clear();
        await using var db = new DungeonWorldContext(options);
        var started = Stopwatch.GetTimestamp();
        await scenario.Run(db);
        if (run >= WarmUps)
            times.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    times.Sort();
    return new QueryTiming(scenario.Name, Percentile(times, 0.5), Percentile(times, 0.95), times[0], recorder.Commands.Count);
}

static double Percentile(List<double> sorted, double share) => sorted[(int)Math.Round(share * (sorted.Count - 1))];

void Compare(string beforeFolder, string afterFolder)
{
    var before = Read(beforeFolder);
    var after = Read(afterFolder);
    Console.WriteLine($"Before: EF Core {before.EfVersion} on .NET {before.Runtime}");
    Console.WriteLine($"After:  EF Core {after.EfVersion} on .NET {after.Runtime}");
    Console.WriteLine();
    Console.WriteLine($"{"Query",-26} {"Before ms",10} {"After ms",9} {"Change",8}  SQL");

    foreach (var timing in after.Timings)
    {
        if (before.Timings.FirstOrDefault(t => t.Name == timing.Name) is not { } old)
        {
            Console.WriteLine($"{timing.Name,-26} {"-",10} {timing.MedianMs,9:F3} {"new",8}");
            continue;
        }

        var change = (timing.MedianMs - old.MedianMs) / old.MedianMs;
        var sql = SameSql(beforeFolder, afterFolder, timing.Name) ? "same" : "changed";
        Console.WriteLine($"{timing.Name,-26} {old.MedianMs,10:F3} {timing.MedianMs,9:F3} {change,8:+0%;-0%;0%}  {sql}");
    }
}

Profile Read(string folder) =>
    JsonSerializer.Deserialize<Profile>(File.ReadAllText(Path.Combine(folder, "results.json")), json)!;

static bool SameSql(string beforeFolder, string afterFolder, string name) =>
    File.ReadAllText(Path.Combine(beforeFolder, "sql", $"{name}.sql")) == File.ReadAllText(Path.Combine(afterFolder, "sql", $"{name}.sql"));

namespace ProfileQueries
{
    /// <param name="Character">Whose sheet to load.</param>
    /// <param name="Fight">The fight to replay, if there's been one.</param>
    /// <param name="Session">The latest game night, if there's been one.</param>
    /// <param name="GameChat">The channel most rolls were made in.</param>
    public record Picks(Snowflake Character, int? Fight, GameSession? Session, Snowflake GameChat);

    public record Scenario(string Name, Func<DungeonWorldContext, Task> Run);

    /// <param name="Commands">How many SQL commands one run sends.</param>
    public record QueryTiming(string Name, double MedianMs, double P95Ms, double MinMs, int Commands);

    public record Profile(string EfVersion, string Runtime, int Runs, IReadOnlyList<QueryTiming> Timings);

    /// <summary>
    /// Notes the SQL of every query sent, without changing anything about how it runs.
    /// </summary>
    public class SqlRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}

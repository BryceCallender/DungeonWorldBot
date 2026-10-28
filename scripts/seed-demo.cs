#!/usr/bin/env dotnet
// Makes a demo copy of the bot's database, full of made-up game nights, for showing the dashboard off.
//
// Copies src/DungeonWorldBot/SanaSolem.db to SanaSolem.demo.db next to it (the real one is only read), then adds ten
// weekly sessions, each with a fight or two played out blow by blow and an evening of rolls in the game chat. Everything
// real (the party, NPCs, rolls, sessions and fights) is kept: the made-up nights go on Fridays clear of the real
// sessions, and every session is renumbered by date. Run it again for a fresh copy; the same seed makes the same nights.
//
//     dotnet run scripts/seed-demo.cs
//
// Then run the dashboard against it with the "dashboard-demo" launch config, or:
//
//     dotnet run --project src/DungeonWorldBot.Dashboard -- --DatabasePath=../DungeonWorldBot/SanaSolem.demo.db
//
// (The path is from the Dashboard's folder, which dotnet run works from.)

#:package Microsoft.Data.Sqlite

using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

const long GameChat = 1325143775139467325;
const long DungeonMaster = 473305025301577728;
const long DiscordEpochMs = 1420070400000;
const int Str = 0, Dex = 1, Con = 2, Int = 3, Wis = 4, Cha = 5;

var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, ".."));
var source = Path.Combine(root, "src", "DungeonWorldBot", "SanaSolem.db");
var demoPath = Path.Combine(root, "src", "DungeonWorldBot", "SanaSolem.demo.db");

// Who's in the party, the stats they lean on, what they roll in a fight and out of one, and how hard they hit
var party = new Dictionary<string, Sheet>
{
    ["Zephyr"] = new(
        new() { [Str] = -1, [Dex] = 2, [Con] = 0, [Int] = 0, [Wis] = 1, [Cha] = -1 },
        [new("Volley", Dex), new("Called Shot", Dex), new("Volley", Dex)],
        [new("Defy Danger", Dex), new("Discern Realities", Wis), new("Hunt and Track", Wis)],
        8),
    ["Mr. Hatter"] = new(
        new() { [Str] = 2, [Dex] = 0, [Con] = 1, [Int] = 0, [Wis] = -1, [Cha] = 0 },
        [new("Hack and Slash", Str), new("Hack and Slash", Str), new("Defend", Con)],
        [new("Read the Room", Int), new("Parley", Cha), new("Defy Danger", Con)],
        10),
    ["Ziedrich Floran"] = new(
        new() { [Str] = -1, [Dex] = 0, [Con] = 0, [Int] = 2, [Wis] = 1, [Cha] = 0 },
        [new("Cast a Spell", Int), new("Cast a Spell", Int), new("Defy Danger", Dex)],
        [new("Spout Lore", Int), new("Discern Realities", Wis), new("Parley", Cha)],
        6),
    ["Edena Trifur"] = new(
        new() { [Str] = 0, [Dex] = 1, [Con] = 0, [Int] = -1, [Wis] = 2, [Cha] = 0 },
        [new("Shapeshift", Wis), new("Hack and Slash", Str), new("Cast a Spell", Wis)],
        [new("Spout Lore", Int), new("Discern Realities", Wis), new("Defy Danger", Dex)],
        6),
};

// What the party runs into
var monsters = new Dictionary<string, Monster>
{
    ["Goblin"] = new(3, 1, 6, "Stab"),
    ["Goblin Orkaster"] = new(5, 0, 8, "Firebolt"),
    ["Bandit"] = new(6, 1, 6, "Rusty Blade"),
    ["Bandit Captain"] = new(12, 2, 10, "Cleave"),
    ["Dire Wolf"] = new(8, 1, 8, "Savage Bite"),
    ["Skeleton"] = new(7, 1, 6, "Bone Club"),
    ["Ghoul"] = new(10, 1, 8, "Paralysing Claws"),
    ["Owlbear"] = new(16, 2, 10, "Crushing Hug"),
    ["Cultist"] = new(5, 0, 6, "Ritual Dagger"),
    ["Cult Leader Varrow"] = new(14, 1, 10, "Shadow Lash"),
    ["Bog Hag"] = new(14, 1, 10, "Drowning Curse"),
    ["Swamp Troll"] = new(20, 3, 10, "Club Swing"),
    ["Wight"] = new(12, 2, 8, "Life Drain"),
    ["Young Black Dragon"] = new(24, 4, 12, "Acid Breath"),
};

// What the party's NPCs do when they join in, and whether it hurts
var npcMoves = new Dictionary<string, NpcMove[]>
{
    ["Elden Notetaker"] = [new("Magic Missile", true), new("Counterspell", false), new("Arcane Shield", false)],
    ["Serena"] = [new("Shortbow", true), new("Bandage", false)],
    ["Jin"] = [new("Knife Throw", true), new("Distract", false)],
};

// Each night: its title and the fights on it, with who turned up to each
Night[] nights =
[
    new("The Road to Mirefen", [new("Ambush at Thornwood", "Thornwood Road", ["Goblin", "Goblin", "Goblin", "Goblin Orkaster"])]),
    new("Wolves at the Gate", [new("Siege of the Waystation", "Greyhollow Waystation", ["Dire Wolf", "Dire Wolf", "Dire Wolf"])]),
    new("A Toll Unpaid", [new("Bridge Toll", "Mirefen Bridge", ["Bandit", "Bandit", "Bandit"]),
                          new("The Captain's Last Stand", "Bandit Camp", ["Bandit Captain", "Bandit", "Bandit"])]),
    new("Bones in the Barrow", [new("The Barrow Door", "Old King's Barrow", ["Skeleton", "Skeleton", "Skeleton", "Skeleton"])]),
    new("Down Among the Dead", [new("Ghouls in the Ossuary", "The Ossuary", ["Ghoul", "Ghoul"]),
                                new("The Wight-Lord Wakes", "Barrow Throne Room", ["Wight", "Skeleton", "Skeleton"])]),
    new("Feathers and Fur", [new("Owlbear!", "Whisperpine Glade", ["Owlbear"])]),
    new("The Hooded Choir", [new("Raid on the Chapel", "Ruined Chapel", ["Cultist", "Cultist", "Cultist", "Cultist"])]),
    new("Varrow's Rite", [new("Stop the Ritual", "Under-Chapel", ["Cult Leader Varrow", "Cultist", "Cultist"])]),
    new("Into the Mire", [new("Troll Under the Causeway", "Sunken Causeway", ["Swamp Troll"]),
                          new("The Hag's Bargain", "Hag's Hut", ["Bog Hag", "Dire Wolf"])]),
    new("Black Wings over Mirefen", [new("The Dragon's Lair", "Drowned Tower", ["Young Black Dragon", "Cultist", "Cultist"])]),
];

var rng = new Random(1325);
T Pick<T>(IReadOnlyList<T> from) => from[rng.Next(from.Count)];
double Between(double min, double max) => min + rng.NextDouble() * (max - min);

File.Delete(demoPath);
using (var real = new SqliteConnection($"Data Source={source};Mode=ReadOnly"))
using (var copy = new SqliteConnection($"Data Source={demoPath}"))
{
    real.Open();
    copy.Open();
    real.BackupDatabase(copy);
}
// Not pooled, so the file's let go of as soon as it's done and a running dashboard can open it
using var db = new SqliteConnection($"Data Source={demoPath};Pooling=False");
db.Open();
using var transaction = db.BeginTransaction();

SqliteCommand Command(string sql, object?[] args)
{
    var command = db.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = sql;
    for (var i = 0; i < args.Length; i++)
        command.Parameters.AddWithValue($"@p{i}", args[i] ?? DBNull.Value);
    return command;
}
void Exec(string sql, params object?[] args) => Command(sql, args).ExecuteNonQuery();
long Insert(string sql, params object?[] args)
{
    Exec(sql, args);
    return (long)Command("SELECT last_insert_rowid()", []).ExecuteScalar()!;
}
long Count(string table) => (long)Command($"SELECT COUNT(*) FROM {table}", []).ExecuteScalar()!;
Dictionary<string, long> Names(string table)
{
    var names = new Dictionary<string, long>();
    using var reader = Command($"SELECT ID, Name FROM {table}", []).ExecuteReader();
    while (reader.Read())
        names[reader.GetString(1)] = reader.GetInt64(0);
    return names;
}

var characters = Names("Characters");
var npcs = Names("Npcs");
var missing = party.Keys.Where(name => !characters.ContainsKey(name)).Order().ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine($"Not in the database: {string.Join(", ", missing)}");
    return 1;
}

foreach (var (name, monster) in monsters)
    Exec("INSERT OR IGNORE INTO MonsterTemplates (Name, MaxHP, Armor, Damage) VALUES (@p0, @p1, @p2, @p3)",
        name, monster.HP, monster.Armor, $"d{monster.Die}");

var usedIDs = new HashSet<long>();

// A Discord message ID made at that time, which is where the dashboard reads a roll's time from
long Snowflake(DateTimeOffset at)
{
    while (true)
    {
        var id = ((at.ToUnixTimeMilliseconds() - DiscordEpochMs) << 22) | (uint)rng.Next(1 << 22);
        if (usedIDs.Add(id))
            return id;
    }
}

DiceRoll Roll(long user, DateTimeOffset at, string? move = null, int? stat = null, int modifier = 0)
{
    int first = rng.Next(1, 7), second = rng.Next(1, 7);
    var total = first + second + modifier;
    var message = Snowflake(at);
    Exec("INSERT INTO Rolls (MessageID, ChannelID, UserID, First, Second, Total, Move, Stat, Modifier) " +
         "VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
        message, GameChat, user, first, second, total, move, stat, modifier);
    return new DiceRoll(message, total);
}

var rollsBefore = Count("Rolls");
var fightsMade = 0;

// Fridays at 7pm Pacific, weekly from August, skipping any within a couple of days of a real session
var realSessions = new List<DateTimeOffset>();
using (var reader = Command("SELECT StartedAt FROM Sessions", []).ExecuteReader())
    while (reader.Read())
        realSessions.Add(DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture));
var nightStarts = new List<DateTimeOffset>();
for (var friday = new DateTimeOffset(2026, 8, 8, 2, 0, 0, TimeSpan.Zero); nightStarts.Count < nights.Length; friday = friday.AddDays(7))
    if (!realSessions.Any(real => Math.Abs((real - friday).TotalDays) < 2))
        nightStarts.Add(friday);

for (var week = 0; week < nights.Length; week++)
{
    var night = nights[week];
    var start = nightStarts[week].AddMinutes(rng.Next(-10, 21));
    var length = TimeSpan.FromHours(Between(3, 4.5));
    var end = start + length;
    Exec("INSERT INTO Sessions (Title, StartedAt, EndedAt) VALUES (@p0, @p1, @p2)", night.Title, Stamp(start), Stamp(end));

    // Fights spread through the night, with exploring and talking around them
    var fightStarts = night.Fights.Select((_, i) => start + length * Between(0.2 + i * 0.4, 0.4 + i * 0.4)).Order().ToList();
    var busy = new List<Span>();

    for (var f = 0; f < night.Fights.Length; f++)
    {
        var plan = night.Fights[f];
        var at = fightStarts[f];
        fightsMade++;
        var encounter = Insert("INSERT INTO Encounters (Name, StartedAt, AnnouncedAt, SceneName) VALUES (@p0, @p1, @p2, @p3)",
            plan.Name, Stamp(at), Stamp(at.AddSeconds(40)), plan.Scene);

        // Several of the same monster are numbered, as the bot does
        var seen = new Dictionary<string, int>();
        var enemies = new List<Combatant>();
        foreach (var kind in plan.Roster)
        {
            var monster = monsters[kind];
            seen[kind] = seen.GetValueOrDefault(kind) + 1;
            var label = plan.Roster.Count(r => r == kind) > 1 ? $"{kind} {seen[kind]}" : kind;
            var id = Insert("INSERT INTO Enemies (Name, EncounterID, CurrentHP, MaxHP, Armor, Damage) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
                label, encounter, monster.HP, monster.HP, monster.Armor, $"d{monster.Die}");
            enemies.Add(new Combatant(id, label, monster) { HP = monster.HP });
        }

        foreach (var who in party.Keys)
            Exec("INSERT INTO EncounterParticipants (EncounterID, CharacterID) VALUES (@p0, @p1)", encounter, characters[who]);
        var helpers = npcMoves.Keys.Where(npcs.ContainsKey).ToList();
        var helper = helpers.Count > 0 && rng.NextDouble() < 0.6 ? Pick(helpers) : null;
        if (helper is not null)
            Exec("INSERT INTO EncounterParticipants (EncounterID, NpcID) VALUES (@p0, @p1)", encounter, npcs[helper]);

        void Log(DateTimeOffset when, string action, string actor, string? roll = null, string? target = null, string? result = null,
            long? character = null, long? enemy = null, long? npc = null, long? message = null) =>
            Exec("INSERT INTO EncounterActions (Action, Actor, At, CharacterID, EncounterID, EnemyID, MessageID, NpcID, Result, Roll, Target) " +
                 "VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10)",
                action, actor, Stamp(when), character, encounter, enemy, message, npc, result, roll, target);

        int Hurt(Combatant enemy, int damage)
        {
            // Always at least a scratch, so the replays don't fill up with hits that did nothing
            var taken = Math.Max(1, damage - enemy.Monster.Armor);
            enemy.HP = Math.Max(0, enemy.HP - taken);
            return taken;
        }

        List<Combatant> Standing() => enemies.Where(e => e.HP > 0).ToList();

        var clock = at.AddMinutes(2);
        for (var round = 0; round < 12 && Standing().Count > 0; round++)
        {
            foreach (var who in party.Keys.OrderBy(_ => rng.Next()))
            {
                if (Standing() is not { Count: > 0 } standing)
                    break;
                var sheet = party[who];
                var move = Pick(sheet.Attacks);
                clock = clock.AddSeconds(rng.Next(40, 151));
                var rolled = Roll(characters[who], clock, move.Name, move.Stat, sheet.Mods[move.Stat]);
                var target = Pick(standing);
                var rollText = $"{rolled.Total} · {Outcome(rolled.Total)}";
                if (move.Name == "Defend")
                    Log(clock, move.Name, who, rollText, character: characters[who], message: rolled.Message,
                        result: rolled.Total >= 7 ? "🛡️ holds the line" : null);
                else if (rolled.Total >= 7)
                {
                    var damage = rng.Next(1, sheet.Damage + 1) + (rolled.Total >= 10 && move.Name == "Called Shot" ? 2 : 0);
                    Log(clock, move.Name, who, rollText, target.Name, $"💥 {Hurt(target, damage)} damage", characters[who], message: rolled.Message);
                }
                else
                    Log(clock, move.Name, who, rollText, target.Name, character: characters[who], message: rolled.Message);

                // A miss (and now and then a weak hit) is the GM's chance to hit back
                if ((rolled.Total < 7 || (rolled.Total < 10 && rng.NextDouble() < 0.4)) && Standing() is { Count: > 0 } left)
                {
                    var enemy = Pick(left);
                    var hit = rng.Next(1, enemy.Monster.Die + 1);
                    clock = clock.AddSeconds(rng.Next(30, 91));
                    Log(clock, enemy.Monster.Attack, enemy.Name, $"d{enemy.Monster.Die} = {hit}", who, $"💥 {hit} damage", enemy: enemy.ID);
                }
            }

            if (helper is not null && rng.NextDouble() < 0.5 && Standing() is { Count: > 0 } targets)
            {
                var action = Pick(npcMoves[helper]);
                var total = rng.Next(2, 13) + 1;
                var target = Pick(targets);
                clock = clock.AddSeconds(rng.Next(30, 91));
                var result = action.Damages && total >= 7 ? $"💥 {Hurt(target, rng.Next(1, 7))} damage" : null;
                Log(clock, action.Name, helper, $"{total} · {Outcome(total)}", result is not null || total < 7 ? target.Name : null,
                    result, npc: npcs[helper]);
            }
        }

        // Whatever's left standing runs off, and the fight's over
        foreach (var enemy in enemies)
            Exec("UPDATE Enemies SET CurrentHP = @p0 WHERE ID = @p1", enemy.HP, enemy.ID);
        var ended = clock.AddMinutes(rng.Next(1, 5));
        Exec("UPDATE Encounters SET EndedAt = @p0, EndAnnouncedAt = @p0 WHERE ID = @p1", Stamp(ended), encounter);
        busy.Add(new Span(at, ended));
    }

    // The rest of the night: exploring, talking, looking around, and the GM rolling behind the screen
    var rolls = rng.Next(30, 56);
    for (var i = 0; i < rolls; i++)
    {
        var when = start + length * rng.NextDouble();
        if (busy.Any(b => b.From <= when && when <= b.To))
            continue;
        if (rng.NextDouble() < 0.15)
        {
            Roll(DungeonMaster, when);
            continue;
        }
        var who = Pick(party.Keys.ToList());
        var sheet = party[who];
        if (rng.NextDouble() < 0.25)
            Roll(characters[who], when);
        else
        {
            var move = Pick(sheet.Other);
            Roll(characters[who], when, move.Name, move.Stat, sheet.Mods[move.Stat]);
        }
    }
}

// Sessions are numbered in the order they were added, so they're put back in date order with the made-up ones among
// the real ones
Exec("CREATE TEMP TABLE Nights AS SELECT Title, StartedAt, EndedAt FROM Sessions");
Exec("DELETE FROM Sessions");
Exec("DELETE FROM sqlite_sequence WHERE name = 'Sessions'");
Exec("INSERT INTO Sessions (Title, StartedAt, EndedAt) SELECT Title, StartedAt, EndedAt FROM Nights ORDER BY StartedAt");

var summary = $"{nights.Length} made-up sessions alongside {realSessions.Count} real ones, {fightsMade} fights, {Count("EncounterActions")} fight actions, {Count("Rolls") - rollsBefore} new rolls";
transaction.Commit();
Console.WriteLine($"Made {Path.GetRelativePath(root, demoPath)}: {summary}");
return 0;

static string Stamp(DateTimeOffset at) =>
    at.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "+00:00";

static string Outcome(int total) => total >= 10 ? "Strong Hit (10+)" : total >= 7 ? "Weak Hit (7-9)" : "Miss (6-)";

// Where this script is, so it finds the database wherever it's run from
static string Here([CallerFilePath] string path = "") => path;

record Move(string Name, int Stat);

record Sheet(Dictionary<int, int> Mods, Move[] Attacks, Move[] Other, int Damage);

record Monster(int HP, int Armor, int Die, string Attack);

record NpcMove(string Name, bool Damages);

record FightPlan(string Name, string Scene, string[] Roster);

record Night(string Title, FightPlan[] Fights);

record Combatant(long ID, string Name, Monster Monster)
{
    public int HP { get; set; }
}

record DiceRoll(long Message, int Total);

record Span(DateTimeOffset From, DateTimeOffset To);

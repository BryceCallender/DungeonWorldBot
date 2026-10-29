#!/usr/bin/env dotnet
// Makes a test copy of both databases from scratch, with something in every table, for trying the bot and dashboard
// out without touching the real ones.
//
// Writes src/DungeonWorldBot/SanaSolem.test.db (the bot's) and src/DungeonWorldBot/canvases.test.db (the dashboard's),
// migrated to the latest schema, then fills them with a made-up campaign: a party of five with full sheets (stats,
// gear, moves, resources, bonds, debilities, shapes, goals, notes), NPCs, a bestiary, ten finished game nights with
// fights played out blow by blow, an evening of rolls around each, and a session going on now with a fight still
// running on its scene. Run it again for a fresh copy; the same seed makes the same campaign.
//
//     dotnet run scripts/seed.cs
//
// Then run the dashboard against it with the "dashboard-seed" launch config, or:
//
//     dotnet run --project src/DungeonWorldBot.Dashboard -- --DatabasePath=../DungeonWorldBot/SanaSolem.test.db --CanvasDatabasePath=../DungeonWorldBot/canvases.test.db
//
// (The paths are from the Dashboard's folder, which dotnet run works from.) The live fight is already announced and
// has no moves waiting, so a bot pointed at it by mistake has nothing to post to game chat.

#:project ../src/DungeonWorldBot.Data/DungeonWorldBot.Data.csproj
// EF builds its model by reflection, which file-based apps turn off by default
#:property PublishAot=false

using System.Runtime.CompilerServices;
using System.Text.Json;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

// The bot's and dashboard's defaults, so rolls count toward stats and the DM's rolls show as theirs
const ulong GameChat = 1325143775139467325;
const ulong OtherChannel = 1325143775139467999;
const ulong DungeonMaster = 473305025301577728;
const ulong DiscordEpochMs = 1420070400000;
const string DashboardUrl = "http://localhost:5284";

var root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Here())!, ".."));
var gamePath = Path.Combine(root, "src", "DungeonWorldBot", "SanaSolem.test.db");
var canvasPath = Path.Combine(root, "src", "DungeonWorldBot", "canvases.test.db");

var rng = new Random(1325);
T Pick<T>(IReadOnlyList<T> from) => from[rng.Next(from.Count)];
double Between(double min, double max) => min + rng.NextDouble() * (max - min);

foreach (var path in new[] { gamePath, canvasPath })
    foreach (var file in new[] { path, path + "-shm", path + "-wal" })
        File.Delete(file);

// Not pooled, so the files are let go of as soon as it's done and a running dashboard can open them
using var game = new DungeonWorldContext(new DbContextOptionsBuilder<DungeonWorldContext>()
    .UseSqlite($"Data Source={gamePath};Pooling=False").Options);
using var canvases = new CanvasContext(new DbContextOptionsBuilder<CanvasContext>()
    .UseSqlite($"Data Source={canvasPath};Pooling=False").Options);
game.Database.Migrate();
canvases.Database.Migrate();

// The party, by their players' made-up Discord IDs
var zephyr = new Snowflake(900000000000000001);
var hatter = new Snowflake(900000000000000002);
var ziedrich = new Snowflake(900000000000000003);
var edena = new Snowflake(900000000000000004);
var morrow = new Snowflake(900000000000000005);

List<Character> characters =
[
    Sheet(zephyr, "Zephyr", ClassType.Ranger, Race.Elves, Alignment.ChaoticGood, level: 3, hp: (18, 21), armor: 1,
        stats: [12, 16, 13, 9, 15, 8],
        items: [Gear("Dungeon Rations", 4), new Armor { Name = "Leather Armor", ArmorRating = 1, Weight = 1 },
                new Weapon { Name = "Hunter's Bow", Damage = 0, Weight = 1 }, Gear("Bundle of Arrows", 3, 1),
                Gear("Adventuring Gear", 5, 1), Gear("Coin", 22)],
        abilities: [Move("Hunt and Track"), Move("Called Shot"), Move("Animal Companion"), Move("Command"),
                    Move("Race: Elf"), Move("Wild Empathy", taken: 2), Move("Camouflage", taken: 3)],
        backstory: "Raised by the wolves of Thornwood after the burning of her village. Tracks the men who lit it."),
    Sheet(hatter, "Mr. Hatter", ClassType.Battlemaster, Race.Human, Alignment.LawfulNeutral, level: 3, hp: (24, 26), armor: 2,
        stats: [16, 12, 15, 13, 8, 10],
        items: [Gear("Dungeon Rations", 5), new Armor { Name = "Chainmail", ArmorRating = 2, Weight = 1 },
                new Weapon { Name = "Halberd", Damage = 1, Weight = 2 }, Gear("Top Hat", 1), Gear("Coin", 13)],
        abilities: [Move("Gambit"), Move("Heavy Armor Training"), Move("Tactics"), Move("War Knowledge"),
                    Move("Block and Strike", taken: 2), Move("Battle Leader", taken: 3, uses: (1, 1))],
        resources: [("Gambit", 2)],
        damageModifier: "+1 vs. bandits",
        backstory: "A retired officer of the Mirefen Guard who never stopped wearing the hat."),
    Sheet(ziedrich, "Ziedrich Floran", ClassType.Templar, Race.Elves, Alignment.LawfulGood, level: 2, hp: (11, 20), armor: 1,
        stats: [13, 9, 12, 8, 16, 15],
        items: [Gear("Dungeon Rations", 3), new Armor { Name = "Leather Armor", ArmorRating = 1, Weight = 1 },
                new Weapon { Name = "Warhammer", Damage = 1, Weight = 1 }, Gear("Holy Symbol", 1), Gear("Soothing Balm", 2),
                Gear("Coin", 7)],
        abilities: [Move("Wrath Embodied"), Move("Penance"), Move("Smite"), Move("Divine Force"), Move("Blinding Light"),
                    Move("Consecrated Ground"), Move("Facet: Fanatical Devotion"), Move("Prophetic Dream", taken: 2)],
        resources: [("Wrath", 1), ("Prophecy", 1)],
        backstory: "A templar sent from the cathedral at Vel to burn out the Hooded Choir, and quietly doubting the order."),
    Sheet(edena, "Edena Trifur", ClassType.Druid, Race.Human, Alignment.TrueNeutral, level: 3, hp: (19, 19), armor: 0,
        stats: [10, 13, 12, 8, 16, 11],
        items: [Gear("Dungeon Rations", 6), new Weapon { Name = "Shillelagh", Damage = 0, Weight = 1 },
                new HexedJars { Name = "Hexed Jars", Contents = ["Bog water", "A wight's finger", "Moth wings"] },
                Gear("Halfling Pipeleaf", 2), Gear("Coin", 4)],
        abilities: [Move("Born of the Soil"), Move("By Nature Sustained"), Move("Spirit Tongue"), Move("Studied Essence"),
                    Move("Shapeshift"), Move("Barkskin", taken: 2), Move("Circle of the Eclipse", taken: 3)],
        resources: [("Shapeshift Hold", 2)],
        backstory: "Speaks for the Whisperpine. Came to Mirefen because the trees told her the swamp was sick."),
    Sheet(morrow, "Morrow Vane", ClassType.Necromancer, Race.Human, Alignment.NeutralEvil, level: 2, hp: (0, 16), armor: 0,
        stats: [8, 13, 14, 16, 12, 9],
        items: [Gear("Healing Potion", 1), new Weapon { Name = "Bone Dagger", Damage = 0, Weight = 1 },
                Gear("Bag of Teeth", 1), Gear("Adventuring Gear", 3, 1), Gear("Coin", 31)],
        abilities: [Move("Replacement Parts"), Move("Corpse Collector"), Move("Flesh Wound", uses: (0, 1)),
                    Move("Curse", taken: 2)],
        resources: [("Power", 3)],
        backstory: "Dug up more than he should have in the Old King's Barrow. Down at 0 HP after the dragon's breath."),
];

// Who's bonded to who, both ways round for some
(Snowflake From, Snowflake To)[] bonds =
[
    (zephyr, edena), (zephyr, hatter), (hatter, zephyr), (hatter, ziedrich), (ziedrich, morrow), (edena, zephyr),
    (morrow, ziedrich), (morrow, edena),
];
foreach (var (from, to) in bonds)
    characters.Single(c => c.ID == from).Bonds!.Add(new Bond { TargetID = to, TargetName = characters.Single(c => c.ID == to).Name });

Character Find(Snowflake id) => characters.Single(c => c.ID == id);
Find(ziedrich).Debilities!.Add(new Debility { StatType = StatType.Con, Modifier = -1 });
Find(morrow).Debilities!.AddRange([new Debility { StatType = StatType.Str, Modifier = -1 }, new Debility { StatType = StatType.Int, Modifier = -1 }]);
Find(morrow).Status = Status.Alive;
Find(hatter).ArmorBonuses!.Add(new ArmorBonus { Source = "Guardian's Stand", Type = ArmorType.Physical, Amount = 1, Duration = ArmorDuration.Fight });
Find(ziedrich).ArmorBonuses!.Add(new ArmorBonus { Source = "Blessed Ward", Type = ArmorType.Divine, Amount = 2, Duration = ArmorDuration.UntilRemoved });
Find(ziedrich).DivineArmor = 1;
Find(edena).MagicArmor = 1;
Find(edena).Transformations!.AddRange([
    new() { Name = "Gray Wolf", Description = "Keen nose, fast over open ground; no hands.", Moves = "Track by scent; Pounce and pin; Howl for the pack", TimesTaken = 6 },
    new() { Name = "Marsh Heron", Description = "Flies, wades, sees fish under murky water.", Moves = "Take to the air; Spear a small creature", TimesTaken = 2, IsActive = true },
    new() { Name = "Owlbear", Description = "Studied essence. Huge and strong; terrible eyesight.", Moves = "Crushing hug; Rend armor; Terrifying screech" },
]);
foreach (var character in characters)
{
    character.Goals!.AddRange([new() { Text = $"Find out who hired the bandits on the Mirefen road" }, new() { Text = "Earn enough coin to rebuild the waystation", Completed = rng.NextDouble() < 0.5 }]);
    character.Notes!.AddRange([
        new() { Text = "Varrow mentioned a \"drowned tower\" before he fled.", CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new() { Text = "The hag owes us a favor. **Don't** cash it in lightly.", CreatedAt = DateTime.UtcNow.AddDays(-9) },
    ]);
}
game.Characters.AddRange(characters);

List<Npc> npcs =
[
    new() { Name = "Elden Notetaker", Role = "Hireling", CurrentHP = 8, MaxHP = 10, Armor = 0, Damage = "d4", Loyalty = 1,
            Skills = "Adept +2, Burglar +1", Cost = "Knowledge", Int = 2, Wis = 1, Dex = 1,
            Notes = "Writes everything down. Has opinions about the party's spelling." },
    new() { Name = "Serena", Role = "Guide", CurrentHP = 12, MaxHP = 12, Armor = 1, Damage = "d6", Loyalty = 2,
            Skills = "Tracker +2, Protector +1", Cost = "Money", Dex = 2, Wis = 1, Notes = "Knows every path through the Mire." },
    new() { Name = "Jin", Role = "Rescued prisoner", CurrentHP = 4, MaxHP = 9, Armor = 0, Damage = "d4", Loyalty = 0,
            Cost = "Revenge", Dex = 1, Cha = 1, Notes = "Escaped the cult. Jumpy." },
    new() { Name = "Ash", Role = "Animal companion", CurrentHP = 10, MaxHP = 10, Armor = 1, Damage = "d6", Str = 1, Dex = 2,
            FollowsID = zephyr, Notes = "A gray wolf. Ferocity +2, Cunning +1." },
    new() { Name = "The Pale Thrall", Role = "Thrall", CurrentHP = 6, MaxHP = 11, Armor = 0, Damage = "d8", Str = 2, Con = 1, Int = -2,
            FollowsID = morrow, Notes = "Mostly one bandit. Partly two." },
];
game.Npcs.AddRange(npcs);

// The bestiary, and what each hits with in the fights below
Dictionary<string, (MonsterTemplate Template, int Die, string Attack)> bestiary = new[]
{
    Monster("Goblin", 3, 1, 6, "Stab", "Horde, Small, Intelligent, Organized", "To multiply", "Charge!; Call more goblins; Retreat and return with more"),
    Monster("Goblin Orkaster", 5, 0, 8, "Firebolt", "Solitary, Small, Magical, Intelligent", "To lead the horde", "Unleash a poorly understood spell; Draw power from his followers"),
    Monster("Bandit", 6, 1, 6, "Rusty Blade", "Horde, Intelligent, Organized", "To rob", "Demand a toll; Gang up; Flee when outnumbered"),
    Monster("Bandit Captain", 12, 2, 10, "Cleave", "Solitary, Intelligent, Organized", "To command", "Order the bandits forward; Fight dirty; Offer a deal"),
    Monster("Dire Wolf", 8, 1, 8, "Savage Bite", "Group, Large", "To hunt", "Drag someone down; Circle and wait; Howl"),
    Monster("Skeleton", 7, 1, 6, "Bone Club", "Horde, Construct", "To guard", "Reassemble; Block the way"),
    Monster("Ghoul", 10, 1, 8, "Paralyzing Claws", "Group, Undead", "To feed", "Paralyze with a touch; Gnaw on the fallen"),
    Monster("Owlbear", 16, 2, 10, "Crushing Hug", "Solitary, Large", "To protect its nest", "Crush; Rend armor; Scream"),
    Monster("Cultist", 5, 0, 6, "Ritual Dagger", "Horde, Intelligent, Organized", "To serve the Choir", "Chant; Sacrifice themselves for the leader"),
    Monster("Cult Leader Varrow", 14, 1, 10, "Shadow Lash", "Solitary, Magical, Intelligent, Cautious", "To wake what sleeps", "Command the Choir; Vanish in shadow; Bargain with secrets"),
    Monster("Bog Hag", 14, 1, 10, "Drowning Curse", "Solitary, Magical, Intelligent, Devious", "To make a bargain", "Offer a deal with a cost; Curse; Hide in the water"),
    Monster("Swamp Troll", 20, 3, 10, "Club Swing", "Solitary, Large", "To smash", "Regenerate; Hurl someone into the mud"),
    Monster("Wight", 12, 2, 8, "Life Drain", "Solitary, Undead, Intelligent", "To reign again", "Drain life; Raise the barrow's dead"),
    Monster("Young Black Dragon", 24, 4, 12, "Acid Breath", "Solitary, Huge, Magical, Terrifying", "To hoard", "Breathe acid; Take to the air; Demand tribute"),
    // In the bestiary but never fought, so it shows with no fights
    Monster("Gelatinous Cube", 20, 1, 10, "Engulf", "Solitary, Large, Amorphous", "To clean", "Engulf; Dissolve gear; Sit very still"),
}.ToDictionary(m => m.Template.Name);
game.MonsterTemplates.AddRange(bestiary.Values.Select(m => m.Template));
game.SaveChanges();

// What each character rolls in a fight and out of one, by the stats they lean on
var fightMoves = new Dictionary<Snowflake, (string Move, StatType Stat)[]>
{
    [zephyr] = [("Volley", StatType.Dex), ("Called Shot", StatType.Dex), ("Volley", StatType.Dex)],
    [hatter] = [("Hack and Slash", StatType.Str), ("Hack and Slash", StatType.Str), ("Defend", StatType.Con)],
    [ziedrich] = [("Hack and Slash", StatType.Str), ("Smite", StatType.Wis), ("Defy Danger", StatType.Dex)],
    [edena] = [("Shapeshift", StatType.Wis), ("Hack and Slash", StatType.Str), ("Defy Danger", StatType.Dex)],
    [morrow] = [("Curse", StatType.Int), ("Hack and Slash", StatType.Int), ("Defy Danger", StatType.Con)],
};
var otherMoves = new Dictionary<Snowflake, (string Move, StatType Stat)[]>
{
    [zephyr] = [("Hunt and Track", StatType.Wis), ("Discern Realities", StatType.Wis), ("Defy Danger", StatType.Dex)],
    [hatter] = [("Parley", StatType.Cha), ("Spout Lore", StatType.Int), ("Defy Danger", StatType.Con)],
    [ziedrich] = [("Discern Realities", StatType.Wis), ("Parley", StatType.Cha), ("Defy Danger", StatType.Wis)],
    [edena] = [("Spout Lore", StatType.Int), ("Discern Realities", StatType.Wis), ("Spirit Tongue", StatType.Wis)],
    [morrow] = [("Spout Lore", StatType.Int), ("Discern Realities", StatType.Wis), ("Parley", StatType.Cha)],
};
var damageDie = characters.ToDictionary(c => c.ID, c => int.Parse(c.Class.Damage[1..]));
int Mod(Snowflake who, StatType stat) => Find(who).Stats.Single(s => s.StatType == stat).Modifier;

// What the NPCs do when they join in, and whether it hurts
var npcMoves = new Dictionary<string, (string Name, bool Damages)[]>
{
    ["Elden Notetaker"] = [("Magic Missile", true), ("Counterspell", false)],
    ["Serena"] = [("Shortbow", true), ("Bandage", false)],
    ["Jin"] = [("Knife Throw", true), ("Distract", false)],
    ["Ash"] = [("Bite", true), ("Trip", false)],
};

// Each night: its title and its fights, with who turned up to each. The last is going on now.
(string Title, (string Name, string Scene, string[] Roster)[] Fights)[] nights =
[
    ("The Road to Mirefen", [("Ambush at Thornwood", "Thornwood Road", ["Goblin", "Goblin", "Goblin", "Goblin Orkaster"])]),
    ("Wolves at the Gate", [("Siege of the Waystation", "Grayhollow Waystation", ["Dire Wolf", "Dire Wolf", "Dire Wolf"])]),
    ("A Toll Unpaid", [("Bridge Toll", "Mirefen Bridge", ["Bandit", "Bandit", "Bandit"]),
                       ("The Captain's Last Stand", "Bandit Camp", ["Bandit Captain", "Bandit", "Bandit"])]),
    ("Bones in the Barrow", [("The Barrow Door", "Old King's Barrow", ["Skeleton", "Skeleton", "Skeleton", "Skeleton"])]),
    ("Down Among the Dead", [("Ghouls in the Ossuary", "The Ossuary", ["Ghoul", "Ghoul"]),
                             ("The Wight-Lord Wakes", "Barrow Throne Room", ["Wight", "Skeleton", "Skeleton"])]),
    ("Feathers and Fur", [("Owlbear!", "Whisperpine Glade", ["Owlbear"])]),
    ("The Hooded Choir", [("Raid on the Chapel", "Ruined Chapel", ["Cultist", "Cultist", "Cultist", "Cultist"])]),
    ("Varrow's Rite", [("Stop the Ritual", "Under-Chapel", ["Cult Leader Varrow", "Cultist", "Cultist"])]),
    ("Into the Mire", [("Troll Under the Causeway", "Sunken Causeway", ["Swamp Troll"]),
                       ("The Hag's Bargain", "Hag's Hut", ["Bog Hag", "Dire Wolf"])]),
    ("Black Wings over Mirefen", [("The Dragon's Lair", "Drowned Tower", ["Young Black Dragon", "Cultist", "Cultist"])]),
    ("The Tower Falls", [("Varrow's Return", "Drowned Tower Summit", ["Cult Leader Varrow", "Cultist", "Cultist", "Ghoul"])]),
];

var usedIDs = new HashSet<ulong>();

// A Discord message ID made at that time, which is where the dashboard reads a roll's time from
Snowflake MessageAt(DateTimeOffset at)
{
    while (true)
    {
        var id = (((ulong)at.ToUnixTimeMilliseconds() - DiscordEpochMs) << 22) | (uint)rng.Next(1 << 22);
        if (usedIDs.Add(id))
            return new Snowflake(id);
    }
}

RollRecord Roll(ulong user, DateTimeOffset at, string? move = null, StatType? stat = null, int modifier = 0, ulong channel = GameChat)
{
    int first = rng.Next(1, 7), second = rng.Next(1, 7);
    var roll = new RollRecord
    {
        MessageID = MessageAt(at), ChannelID = new Snowflake(channel), UserID = new Snowflake(user),
        First = first, Second = second, Modifier = modifier, Total = first + second + modifier, Move = move, Stat = stat,
        Modifiers = stat is { } s ? $"{s.ToString().ToUpper()} {modifier:+0;-0;+0}" : null,
    };
    // Now and then someone changes the outcome for a rule the bot doesn't track
    if (roll.Total is >= 5 and <= 6 && rng.NextDouble() < 0.05)
        (roll.ChangedOutcome, roll.ChangedReason, roll.ChangedBy) = (MoveOutcome.WeakHit, "Blessing of Luna", new Snowflake(DungeonMaster));
    game.Rolls.Add(roll);
    return roll;
}

// Weekly on Fridays at 7pm Pacific, the last finished one a week before tonight's
var now = DateTimeOffset.UtcNow;
var tonight = now.AddMinutes(-75);
var fightCount = 0;
var actionCount = 0;
Encounter? liveFight = null;
List<Enemy> liveEnemies = [];
var party = characters.Select(c => c.ID).ToList();

for (var week = 0; week < nights.Length; week++)
{
    var (title, fights) = nights[week];
    var live = week == nights.Length - 1;
    var start = live ? tonight : new DateTimeOffset(now.Date, TimeSpan.Zero).AddDays(-7 * (nights.Length - 1 - week)).AddHours(2).AddMinutes(rng.Next(-10, 21));
    var length = live ? now - start : TimeSpan.FromHours(Between(3, 4.5));
    game.Sessions.Add(new GameSession { Title = title, StartedAt = start, EndedAt = live ? null : start + length });

    // Fights spread through the night, with exploring and talking around them
    var fightStarts = fights.Select((_, i) => start + length * (live ? 0.3 : Between(0.2 + i * 0.4, 0.4 + i * 0.4))).ToList();
    List<(DateTimeOffset From, DateTimeOffset To)> busy = [];

    for (var f = 0; f < fights.Length; f++)
    {
        var (name, scene, roster) = fights[f];
        var at = fightStarts[f];
        var still = live && f == fights.Length - 1;
        fightCount++;

        // Several of the same monster are numbered, as the bot does
        var seen = new Dictionary<string, int>();
        List<Enemy> enemies = [];
        foreach (var kind in roster)
        {
            var template = bestiary[kind].Template;
            seen[kind] = seen.GetValueOrDefault(kind) + 1;
            enemies.Add(new Enemy
            {
                Name = roster.Count(r => r == kind) > 1 ? $"{kind} {seen[kind]}" : kind,
                CurrentHP = template.MaxHP, MaxHP = template.MaxHP, Armor = template.Armor, Damage = template.Damage,
                Tags = template.Tags, Instinct = template.Instinct, Moves = template.Moves, Notes = template.Notes,
            });
        }
        var encounter = new Encounter { Name = name, StartedAt = at, AnnouncedAt = at.AddSeconds(40), SceneName = scene, Enemies = enemies };
        // Everyone, or (for some fights) only those the DM picked, NPCs included
        var helper = rng.NextDouble() < 0.6 ? Pick(npcs.Where(n => npcMoves.ContainsKey(n.Name)).ToList()) : null;
        var fighters = week % 3 == 1 ? party.Where(p => p != morrow).ToList() : party;
        if (week % 4 != 0 || helper is not null)
        {
            encounter.Participants.AddRange(fighters.Select(p => new EncounterParticipant { CharacterID = p }));
            if (helper is not null)
                encounter.Participants.Add(new EncounterParticipant { NpcID = helper.ID });
        }
        game.Encounters.Add(encounter);
        // The enemies need their IDs before anything can be logged against them
        game.SaveChanges();

        void Log(DateTimeOffset when, string action, string actor, string? roll = null, string? target = null, string? result = null,
            Snowflake? character = null, int? enemy = null, int? npc = null, Snowflake? message = null)
        {
            actionCount++;
            encounter.Actions.Add(new EncounterAction
            {
                At = when, Action = action, Actor = actor, Roll = roll, Target = target, Result = result,
                CharacterID = character, EnemyID = enemy, NpcID = npc, MessageID = message,
            });
        }

        int Hurt(Enemy enemy, int damage)
        {
            // Always at least a scratch, so the replays don't fill up with hits that did nothing
            var taken = Math.Max(1, damage - enemy.Armor);
            enemy.CurrentHP = Math.Max(0, enemy.CurrentHP - taken);
            return taken;
        }

        List<Enemy> Standing() => enemies.Where(e => e.CurrentHP > 0).ToList();

        var clock = at.AddMinutes(2);
        // A fight still going on gets a couple of rounds in, so its enemies are hurt but standing
        for (var round = 0; round < (still ? 2 : 12) && Standing().Count > 0; round++)
        {
            foreach (var who in fighters.OrderBy(_ => rng.Next()))
            {
                if (Standing() is not { Count: > 0 } standing)
                    break;
                var character = Find(who);
                var (move, stat) = Pick(fightMoves[who]);
                clock = clock.AddSeconds(rng.Next(40, 151));
                var rolled = Roll(who.Value, clock, move, stat, Mod(who, stat));
                var target = Pick(standing);
                var rollText = $"{rolled.Total} · {Outcome(rolled.Total)}";
                if (move is "Defend" or "Defy Danger")
                    Log(clock, move, character.Name, rollText, character: who, message: rolled.MessageID,
                        result: rolled.Total >= 7 && move == "Defend" ? "🛡️ holds the line" : null);
                else if (rolled.Total >= 7)
                {
                    var damage = rng.Next(1, damageDie[who] + 1) + (rolled.Total >= 10 && move == "Called Shot" ? 2 : 0);
                    Log(clock, move, character.Name, rollText, target.Name, $"💥 {Hurt(target, damage)} damage", who, message: rolled.MessageID);
                }
                else
                    Log(clock, move, character.Name, rollText, target.Name, character: who, message: rolled.MessageID);

                // A miss (and now and then a weak hit) is the GM's chance to hit back
                if ((rolled.Total < 7 || (rolled.Total < 10 && rng.NextDouble() < 0.4)) && Standing() is { Count: > 0 } left)
                {
                    var enemy = Pick(left);
                    var (_, die, attack) = bestiary[roster[enemies.IndexOf(enemy)]];
                    var hit = rng.Next(1, die + 1);
                    clock = clock.AddSeconds(rng.Next(30, 91));
                    Log(clock, attack, enemy.Name, $"d{die} = {hit}", character.Name, $"💥 {hit} damage", enemy: enemy.ID);
                }
            }

            if (helper is not null && rng.NextDouble() < 0.5 && Standing() is { Count: > 0 } targets)
            {
                var (action, damages) = Pick(npcMoves[helper.Name]);
                var total = rng.Next(2, 13) + 1;
                var target = Pick(targets);
                clock = clock.AddSeconds(rng.Next(30, 91));
                var result = damages && total >= 7 ? $"💥 {Hurt(target, rng.Next(1, 7))} damage" : null;
                Log(clock, action, helper.Name, $"{total} · {Outcome(total)}", result is not null || total < 7 ? target.Name : null,
                    result, npc: helper.ID);
            }
        }

        if (still)
        {
            liveFight = encounter;
            liveEnemies = enemies;
            busy.Add((at, now));
            continue;
        }
        // Whatever's left standing runs off, and the fight's over
        var ended = clock.AddMinutes(rng.Next(1, 5));
        encounter.EndedAt = encounter.EndAnnouncedAt = ended;
        busy.Add((at, ended));
    }

    // The rest of the night: exploring, talking, looking around, and the GM rolling behind the screen
    var rolls = live ? 12 : rng.Next(30, 56);
    for (var i = 0; i < rolls; i++)
    {
        var when = start + length * rng.NextDouble();
        if (busy.Any(b => b.From <= when && when <= b.To))
            continue;
        var who = Pick(party);
        if (rng.NextDouble() < 0.15)
            Roll(DungeonMaster, when);
        else if (rng.NextDouble() < 0.2)
            Roll(who.Value, when);
        else
        {
            var (move, stat) = Pick(otherMoves[who]);
            Roll(who.Value, when, move, stat, Mod(who, stat));
        }
    }
}

// Rolls between sessions and outside game chat, which the dashboard leaves out of sessions and stats
for (var i = 0; i < 15; i++)
    Roll(Pick(party).Value, now.AddDays(-Between(1, 60)), "Defy Danger", StatType.Dex, 1);
for (var i = 0; i < 10; i++)
    Roll(Pick(party).Value, now.AddDays(-Between(1, 60)), channel: OtherChannel);
game.SaveChanges();

// Tonight's fight is on a scene, with everyone's tokens on it and a few moves already made from the board
var (sceneID, sceneName) = SeedCanvases(liveFight!, liveEnemies);
liveFight!.SceneID = CanvasIds.Encode(sceneID);
liveFight.SceneName = sceneName;
liveFight.SceneUrl = $"{DashboardUrl}/scene/{liveFight.SceneID}";
var lastActions = liveFight.Actions.Where(a => a.Action is "Hack and Slash" or "Volley" && a.Target is not null).TakeLast(2).ToList();
foreach (var action in lastActions)
    game.MoveRequests.Add(new MoveRequest
    {
        Encounter = liveFight, RequestedAt = action.At.AddSeconds(-2), RequestedBy = action.CharacterID,
        Kind = action.Action == "Volley" ? MoveRequestKind.Volley : MoveRequestKind.HackAndSlash, CharacterID = action.CharacterID,
        TargetEnemyID = liveEnemies.Single(e => e.Name == action.Target).ID, ResolvedAt = action.At, ActionID = action.ID,
        Summary = action.Result?.Replace("💥 ", "-").Replace(" damage", "") ?? "Miss",
    });
game.MoveRequests.Add(new MoveRequest
{
    Encounter = liveFight, RequestedAt = now.AddMinutes(-3), RequestedBy = morrow, Kind = MoveRequestKind.ClassMove,
    CharacterID = morrow, Move = "Curse", TargetEnemyID = liveEnemies[0].ID, ResolvedAt = now.AddMinutes(-3).AddSeconds(1),
    Error = "Morrow Vane is at 0 HP and can't act.",
});
game.SaveChanges();

Console.WriteLine($"Made {Path.GetRelativePath(root, gamePath)}: {characters.Count} characters, {npcs.Count} NPCs, " +
    $"{bestiary.Count} monsters, {nights.Length} sessions (the last going on now), {fightCount} fights, {actionCount} fight actions, " +
    $"{game.Rolls.Count()} rolls");
Console.WriteLine($"Made {Path.GetRelativePath(root, canvasPath)}: {canvases.Canvases.Count()} canvases, {canvases.CanvasItems.Count()} items");
return 0;

// A scene for the live fight, a map of the region, and one put away. Gives back the scene's ID and name.
(int ID, string Name) SeedCanvases(Encounter fight, List<Enemy> enemies)
{
    var made = DateTime.UtcNow.AddDays(-20);
    var scene = new Canvas { Name = fight.SceneName!, Kind = CanvasKind.Encounter, CreatedAt = made, UpdatedAt = DateTime.UtcNow.AddMinutes(-2) };
    var map = new Canvas { Name = "The Mirefen", Kind = CanvasKind.Map, CreatedAt = made, UpdatedAt = DateTime.UtcNow.AddDays(-3) };
    var old = new Canvas { Name = "Thornwood Road", Kind = CanvasKind.Encounter, CreatedAt = made.AddDays(-40), UpdatedAt = made.AddDays(-40), ArchivedAt = made.AddDays(-30) };
    canvases.Canvases.AddRange(scene, map, old);
    canvases.SaveChanges();

    void Add(Canvas canvas, CanvasItemType type, object json, string author = "encounter") =>
        canvases.CanvasItems.Add(new CanvasItem
        {
            ID = Guid.NewGuid().ToString(), CanvasID = canvas.ID, Type = type, Json = JsonSerializer.Serialize(json),
            AuthorID = author, CreatedAt = DateTime.UtcNow.AddMinutes(-rng.Next(5, 60)),
        });

    // Built as the dashboard's EnemyTokens builds them: a group named for who it is, holding a colored circle
    object Token(string name, double x, double y, string color, string? fighter) => new
    {
        attrs = fighter is null ? (object)new { x, y, name } : new { x, y, name, fighter },
        className = "Group",
        children = new[] { new { attrs = new { radius = 36, fill = color, stroke = "#ffffff", strokeWidth = 2 }, className = "Circle" } },
    };

    for (var i = 0; i < characters.Count; i++)
        Add(scene, CanvasItemType.Token, Token(characters[i].Name, 600 + i * 84, 650, "#378ADD", $"character:{characters[i].ID.Value}"));
    // The NPCs fighting alongside them, in the board's green
    var allies = npcs.Where(n => fight.Participants.Any(p => p.NpcID == n.ID)).ToList();
    for (var i = 0; i < allies.Count; i++)
        Add(scene, CanvasItemType.Token, Token(allies[i].Name, 600 + (characters.Count + i) * 84, 650, "#1D9E75", $"npc:{allies[i].ID}"));
    for (var i = 0; i < enemies.Count; i++)
        Add(scene, CanvasItemType.Token, Token(enemies[i].Name, 640 + i * 84, 350, "#D85A30", $"enemy:{enemies[i].ID}"));
    // A loose token nobody's fighting as
    Add(scene, CanvasItemType.Token, Token("Altar", 800, 200, "#888780", null), DungeonMaster.ToString());
    Add(scene, CanvasItemType.Shape, new
    {
        attrs = new { x = 500, y = 150, width = 600, height = 600, stroke = "#5F5E5A", strokeWidth = 6, cornerRadius = 4 },
        className = "Rect",
    }, DungeonMaster.ToString());
    Add(scene, CanvasItemType.Text, new
    {
        attrs = new { x = 520, y = 160, text = "Tower summit — the floor is crumbling", fontSize = 28, fontFamily = "system-ui, sans-serif", fill = "#B4B2A9" },
        className = "Text",
    }, DungeonMaster.ToString());
    Add(scene, CanvasItemType.Stroke, new
    {
        attrs = new { points = new[] { 700, 620, 720, 540, 760, 470, 790, 400 }, stroke = "#E24B4A", strokeWidth = 5, lineCap = "round", lineJoin = "round" },
        className = "Line",
    }, zephyr.Value.ToString());

    Add(map, CanvasItemType.Text, new
    {
        attrs = new { x = 300, y = 120, text = "THE MIREFEN", fontSize = 48, fontStyle = "bold", fontFamily = "system-ui, sans-serif", fill = "#B4B2A9" },
        className = "Text",
    }, DungeonMaster.ToString());
    (string Name, int X, int Y)[] places = [("Grayhollow Waystation", 250, 300), ("Mirefen Bridge", 550, 380), ("Old King's Barrow", 900, 260),
                                            ("Ruined Chapel", 700, 600), ("Hag's Hut", 1050, 640), ("Drowned Tower", 1250, 420)];
    foreach (var (name, x, y) in places)
        Add(map, CanvasItemType.Token, Token(name, x, y, "#BA7517", null), DungeonMaster.ToString());
    Add(map, CanvasItemType.Stroke, new
    {
        attrs = new { points = places.SelectMany(p => new[] { p.X, p.Y }).ToArray(), stroke = "#854F0B", strokeWidth = 4, lineCap = "round", lineJoin = "round", dash = new[] { 12, 8 } },
        className = "Line",
    }, DungeonMaster.ToString());

    Add(old, CanvasItemType.Token, Token("Goblin 1", 700, 400, "#D85A30", null));
    canvases.SaveChanges();
    return (scene.ID, scene.Name);
}

Character Sheet(Snowflake id, string name, ClassType type, Race race, Alignment alignment, int level, (int Current, int Max) hp,
    int armor, int[] stats, List<Item> items, List<Ability> abilities, (string Name, int Value)[]? resources = null,
    string? damageModifier = null, string? backstory = null)
{
    foreach (var ability in abilities)
        ability.LevelTaken ??= 1;
    return new Character
    {
        ID = id, Name = name, Level = level, ArmorRating = armor, Race = race, Alignment = alignment,
        Status = Status.Alive | Status.Resting, DamageModifier = damageModifier, Backstory = backstory,
        Class = new Class { Type = type },
        Health = new Health { CurrentHP = hp.Current, MaxHP = hp.Max },
        Inventory = new Inventory { Items = items, CurrentLoad = items.Sum(i => i.Weight * i.Amount), MaxLoad = 9 },
        Stats = stats.Select((value, i) => new Stat { StatType = (StatType)i, Value = value }).ToList(),
        Abilities = abilities,
        Resources = (resources ?? []).Select(r => new CharacterResource { Name = r.Name, Value = r.Value }).ToList(),
        Location = new Location { X = rng.Next(0, 100), Y = rng.Next(0, 100) },
        Bonds = [], Debilities = [], ArmorBonuses = [], Transformations = [], Goals = [], Notes = [],
    };
}

static Item Gear(string name, int amount, int weight = 0) => new() { Name = name, Amount = amount, Weight = weight };

static Ability Move(string name, int? taken = null, (int Uses, int Max)? uses = null) =>
    new() { Name = name, LevelTaken = taken, Uses = uses?.Uses ?? 0, MaxUses = uses?.Max };

static (MonsterTemplate Template, int Die, string Attack) Monster(string name, int hp, int armor, int die, string attack,
    string tags, string instinct, string moves) =>
    (new MonsterTemplate { Name = name, MaxHP = hp, Armor = armor, Damage = $"d{die}", Tags = tags, Instinct = instinct, Moves = moves,
        Notes = $"Hits with {attack}." }, die, attack);

static string Outcome(int total) => total >= 10 ? "Strong Hit (10+)" : total >= 7 ? "Weak Hit (7-9)" : "Miss (6-)";

// Where this script is, so it finds the databases wherever it's run from
static string Here([CallerFilePath] string path = "") => path;

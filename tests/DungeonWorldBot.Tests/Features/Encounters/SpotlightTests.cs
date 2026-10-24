using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Rolls;
using Microsoft.Extensions.Configuration;
using Remora.Discord.API;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Encounters;

public sealed class SpotlightTests : IDisposable
{
    private static readonly Snowflake GameChat = DiscordSnowflake.New(222);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static SpotlightService Spotlight(DungeonWorldContext context, EncounterService encounters) =>
        new(context, new RollStatsService(context, GameChatConfig()), encounters, new NpcService(context));

    private static IConfiguration GameChatConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GameChatChannelId"] = GameChat.Value.ToString() }).Build();

    private static Character Hero(ulong id, string name) => new() { ID = DiscordSnowflake.New(id), Name = name, Status = Status.Alive };

    [Fact]
    public async Task EnemyAttacks_ShowWhatTheyDidAndWho_AndTheQuietOneIsDue()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var encounter = (await encounters.StartAsync("Ambush"))!;
        var goblin = new Enemy { Name = "Goblin 1", CurrentHP = 3, MaxHP = 3 };
        await encounters.AddEnemyAsync(encounter, goblin);
        await encounters.AddEnemyAsync(encounter, new Enemy { Name = "Goblin 2", CurrentHP = 3, MaxHP = 3 });
        await encounters.AddEnemyAsync(encounter, new Enemy { Name = "Goblin 3", CurrentHP = 0, MaxHP = 3 });

        await encounters.LogAsync(new EncounterAction
        {
            Actor = goblin.Name, EnemyID = goblin.ID, Action = "Bite", Roll = "d6 = 4", Target = "Aria", Result = "took 3",
        });

        var spotlight = await Spotlight(context, encounters).GetAsync(encounter, []);

        // The defeated one is out of it, and the one who hasn't acted comes first
        Assert.Equal(["Goblin 2", "Goblin 1"], spotlight.Enemies.Select(e => e.Name));
        Assert.True(spotlight.Enemies[0].Quiet);
        var bite = spotlight.Enemies[1].Last!;
        Assert.Equal("Bite · d6 = 4 → Aria · took 3", bite.Summary);
    }

    [Fact]
    public async Task PlayersRolls_TakeTheTargetFromTheLog()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var encounter = (await encounters.StartAsync("Ambush"))!;
        var aria = Hero(1, "Aria");
        var bram = Hero(2, "Bram");

        var logged = Snowflake.CreateTimestampSnowflake(DateTimeOffset.UtcNow.AddSeconds(1), Constants.DiscordEpoch);
        var plain = Snowflake.CreateTimestampSnowflake(DateTimeOffset.UtcNow.AddSeconds(2), Constants.DiscordEpoch);
        context.Rolls.Add(new RollRecord { MessageID = logged, ChannelID = GameChat, UserID = aria.ID, First = 4, Second = 4, Total = 9, Move = "Hack and Slash" });
        context.Rolls.Add(new RollRecord { MessageID = plain, ChannelID = GameChat, UserID = bram.ID, First = 1, Second = 2, Total = 3, Move = "Defy Danger" });
        await context.SaveChangesAsync();
        await encounters.LogAsync(new EncounterAction
        {
            Actor = aria.Name, CharacterID = aria.ID, MessageID = logged, Action = "Hack and Slash",
            Roll = "9 · Weak Hit (7-9)", Target = "Goblin", Result = "took 4",
        });

        var spotlight = await Spotlight(context, encounters).GetAsync(encounter, [aria, bram]);

        Assert.Equal(2, spotlight.Rolls);
        Assert.Equal(1, spotlight.Misses);
        // Aria acted before Bram, so she's due again; her roll isn't counted twice
        Assert.Equal(["Aria", "Bram"], spotlight.Party.Select(p => p.Name));
        Assert.True(spotlight.Party[0].Quiet);
        Assert.Equal(1, spotlight.Party[0].Actions);
        Assert.Equal("Goblin", spotlight.Party[0].Last!.Target);
        Assert.Equal("Defy Danger · 3 · Miss (6-)", spotlight.Party[1].Last!.Summary);
    }

    [Fact]
    public async Task Allies_ShowTheNpcsStillUp_WithWhatTheyDid()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var npcs = new NpcService(context);
        var encounter = (await encounters.StartAsync("Ambush"))!;
        var grub = new Npc { Name = "Grub", CurrentHP = 6, MaxHP = 6 };
        await npcs.AddAsync(grub);
        await npcs.AddAsync(new Npc { Name = "Pip", CurrentHP = 4, MaxHP = 4 });
        await npcs.AddAsync(new Npc { Name = "Down", CurrentHP = 0, MaxHP = 4 });

        await encounters.LogAsync(new EncounterAction
        {
            Actor = grub.Name, NpcID = grub.ID, Action = "Damage", Roll = "d6 = 5", Target = "Goblin", Result = "took 4",
        });

        var spotlight = await Spotlight(context, encounters).GetAsync(encounter, []);

        Assert.Equal(["Pip", "Grub"], spotlight.Allies.Select(a => a.Name));
        Assert.True(spotlight.Allies[0].Quiet);
        Assert.Equal("Damage · d6 = 5 → Goblin · took 4", spotlight.Allies[1].Last!.Summary);
        // NPCs' rolls aren't the party's
        Assert.Equal(0, spotlight.Rolls);
    }

    [Fact]
    public async Task PickedFight_ShowsOnlyWhoWasPicked_AndAnyoneWhoJoinedIn()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        var npcs = new NpcService(context);
        var grub = new Npc { Name = "Grub", CurrentHP = 6, MaxHP = 6 };
        var pip = new Npc { Name = "Pip", CurrentHP = 4, MaxHP = 4 };
        await npcs.AddAsync(grub);
        await npcs.AddAsync(pip);
        var aria = Hero(1, "Aria");
        var bram = Hero(2, "Bram");
        var cass = Hero(3, "Cass");

        var encounter = (await encounters.StartAsync("Ambush", roster: new EncounterRoster([aria.ID], [grub.ID])))!;
        // Cass wasn't picked, but rolled in the fight anyway
        await encounters.LogAsync(new EncounterAction { Actor = cass.Name, CharacterID = cass.ID, Action = "Volley" });

        var spotlight = await Spotlight(context, encounters).GetAsync((await encounters.GetActiveAsync())!, [aria, bram, cass]);

        Assert.Equal(["Aria", "Cass"], spotlight.Party.Select(p => p.Name).Order());
        Assert.Equal(["Grub"], spotlight.Allies.Select(a => a.Name));
    }

    [Fact]
    public async Task UnpickedFight_ShowsEveryone()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);
        await new NpcService(context).AddAsync(new Npc { Name = "Grub", CurrentHP = 6, MaxHP = 6 });
        var encounter = (await encounters.StartAsync("Ambush"))!;

        var spotlight = await Spotlight(context, encounters).GetAsync(encounter, [Hero(1, "Aria"), Hero(2, "Bram")]);

        Assert.Equal(["Aria", "Bram"], spotlight.Party.Select(p => p.Name).Order());
        Assert.Equal(["Grub"], spotlight.Allies.Select(a => a.Name));
    }

    [Fact]
    public async Task Logging_DoesNothingBetweenFights()
    {
        await using var context = _db.CreateContext();
        var encounters = new EncounterService(context);

        await encounters.LogAsync(new EncounterAction { Actor = "Goblin", Action = "Attack" });

        Assert.Empty(context.EncounterActions);
    }
}

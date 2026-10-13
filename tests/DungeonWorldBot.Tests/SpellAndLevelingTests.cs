using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Services.Implementation;
using DungeonWorldBot.Spells;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests;

public class SpellTests
{
    private static Spell Spell(SpellList list, string name) => SpellLists.Find($"{name} ({list})").Single();

    [Fact]
    public void SpellsFitInDiscord()
    {
        Assert.All(SpellLists.All, s =>
        {
            Assert.InRange(s.Name.Length + s.Heading.Length + 3, 1, 256);
            Assert.InRange(s.Description.Length, 1, 1024);
        });
    }

    [Theory]
    [InlineData(SpellList.Cleric, 0, 3)]
    [InlineData(SpellList.Cleric, 1, 7)]
    [InlineData(SpellList.Wizard, 0, 3)]
    [InlineData(SpellList.Wizard, 9, 5)]
    public void ListsHaveEverySpell(SpellList list, int level, int count)
    {
        Assert.Equal(count, SpellLists.For(list).Count(s => s.Level == level));
    }

    [Fact]
    public void SharedNames_ResolveByList()
    {
        Assert.Equal(2, SpellLists.Find("Light").Count);
        Assert.Equal(SpellList.Wizard, SpellLists.Find("light (wizard)").Single().List);
    }

    [Fact]
    public void Learning_RespectsCasterLevelAndTotal()
    {
        var known = new List<Spell> { Spell(SpellList.Cleric, "Cure Light Wounds"), Spell(SpellList.Cleric, "Magic Weapon") };

        // Caster level 1 allows 2 spell levels, and both are used
        Assert.Contains("2 of 2", SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Sanctuary"), 1, known));
        Assert.Null(SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Sanctuary"), 2, known));
        Assert.Contains("above your cleric level", SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Darkness"), 2, []));
        Assert.Null(SpellCasting.WhyCannotLearn(Spell(SpellList.Cleric, "Guidance"), 1, known));
        Assert.Contains("already have", SpellCasting.WhyCannotLearn(known[0], 5, known));
    }

    [Fact]
    public void GodAmidstTheWastes_GivesClericSpells()
    {
        var ranger = new Character
        {
            Class = new Class { Type = ClassType.Ranger },
            Abilities = [new Ability { ID = 1, Name = "God Amidst the Wastes" }],
        };

        var access = Assert.Single(SpellCasting.Access(ranger));
        Assert.Equal(SpellList.Cleric, access.List);
        Assert.Equal("Cleric Level", access.LevelResource.Name);

        Assert.Empty(SpellCasting.Access(new Character { Class = new Class { Type = ClassType.Ranger }, Abilities = [] }));
    }

    [Fact]
    public void KnownSpells_UseTheListNote()
    {
        var character = new Character
        {
            Abilities =
            [
                new Ability { Name = "Light", Description = "...\n" + SpellCasting.ListNote(SpellList.Wizard) },
                new Ability { Name = "Bless", Description = "something unrelated" },
            ],
        };

        Assert.Equal(SpellList.Wizard, Assert.Single(SpellCasting.KnownSpells(character)).List);
    }
}

public sealed class LevelingServiceTests : IDisposable
{
    private static readonly Snowflake UserId = new(77);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public LevelingServiceTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private DungeonWorldContext CreateContext() =>
        new(new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(_connection).Options);

    private static async Task<(Character, LevelingService, ResourceService)> SetupAsync(DungeonWorldContext context, int con = 12)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Throndir",
            Level = 2,
            Class = new Class { Type = ClassType.Ranger },
            Health = new Health { CurrentHP = 20, MaxHP = 20 },
            Inventory = new Inventory(),
            Stats = [new Stat { StatType = StatType.Con, Value = con }, new Stat { StatType = StatType.Dex, Value = 18 }],
            Abilities = [new Ability { Name = "God Amidst the Wastes" }],
        };

        var characters = new CharacterService(context);
        await characters.AddCharacterAsync(character);
        var resources = new ResourceService(context);
        return (character, new LevelingService(characters, resources), resources);
    }

    [Fact]
    public async Task LevellingUp_GivesStatPointsAndRaisesClericLevel()
    {
        await using var context = CreateContext();
        var (character, leveling, resources) = await SetupAsync(context);
        var clericLevel = SpellCasting.Access(character).Single().LevelResource;

        await leveling.ChangeLevelAsync(character, 4);

        Assert.Equal(4, character.Level);
        Assert.Equal(2, leveling.GetStatPoints(character));
        Assert.Equal(3, resources.GetValue(character, clericLevel));

        await leveling.ChangeLevelAsync(character, 3);
        Assert.Equal(2, leveling.GetStatPoints(character));
        Assert.Equal(2, resources.GetValue(character, clericLevel));
    }

    [Fact]
    public async Task AllocatingStats_SpendsPointsAndUpdatesHp()
    {
        await using var context = CreateContext();
        var (character, leveling, _) = await SetupAsync(context);

        Assert.NotNull(await leveling.AllocateStatAsync(character, StatType.Con));

        await leveling.GrantStatPointsAsync(character, 1);
        Assert.Null(await leveling.AllocateStatAsync(character, StatType.Con));

        Assert.Equal(13, character.Stats.Single(s => s.StatType == StatType.Con).Value);
        Assert.Equal(character.Class.HealthModifier + 13, character.Health.MaxHP);
        Assert.Equal(0, leveling.GetStatPoints(character));
    }

    [Fact]
    public async Task AllocatingStats_StopsAt18()
    {
        await using var context = CreateContext();
        var (character, leveling, _) = await SetupAsync(context);
        await leveling.GrantStatPointsAsync(character, 1);

        Assert.Contains("can't go above 18", await leveling.AllocateStatAsync(character, StatType.Dex));
        Assert.Equal(1, leveling.GetStatPoints(character));
    }
}

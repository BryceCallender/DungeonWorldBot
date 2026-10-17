using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Spells;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Leveling;

public sealed class LevelingServiceTests : IDisposable
{
    private static readonly Snowflake UserId = new(77);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

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
    public async Task ClericLevel_CountsFromTheLevelTheMoveWasTaken()
    {
        await using var context = CreateContext();
        var (character, leveling, resources) = await SetupAsync(context);
        character.Abilities![0].LevelTaken = 2;
        var clericLevel = SpellCasting.Access(character).Single().LevelResource;

        Assert.Equal(1, resources.GetValue(character, clericLevel));

        await leveling.ChangeLevelAsync(character, 5);
        Assert.Equal(4, resources.GetValue(character, clericLevel));
        Assert.Equal(2, character.Abilities[0].LevelTaken);
    }

    [Fact]
    public async Task SettingClericLevel_RecordsWhenTheMoveWasTaken()
    {
        await using var context = CreateContext();
        var (character, leveling, resources) = await SetupAsync(context);
        var clericLevel = SpellCasting.Access(character).Single().LevelResource;

        // Taken before levels were recorded: set it once and it follows from then on
        Assert.Null(character.Abilities![0].LevelTaken);
        Assert.Equal(2, await resources.SetValueAsync(character, clericLevel, 2));
        Assert.Equal(1, character.Abilities[0].LevelTaken);

        await leveling.ChangeLevelAsync(character, 3);
        Assert.Equal(3, resources.GetValue(character, clericLevel));
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

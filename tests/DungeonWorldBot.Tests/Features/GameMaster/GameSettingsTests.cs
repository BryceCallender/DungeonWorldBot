using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.GameMaster;

public sealed class GameSettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"game-settings-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    private GameSettingsService Service() => new(Options.Create(new BotSettings { GameSettingsPath = _path }));

    [Fact]
    public async Task StatPoints_DefaultToOnePerLevel_AndCanBeChanged()
    {
        var settings = Service();
        Assert.Equal(1, settings.StatPointsForLevel(5));

        await settings.SetStatPointsForLevelAsync(4, 2);
        Assert.Equal(2, settings.StatPointsForLevel(4));
        Assert.Equal(1, settings.StatPointsForLevel(5));

        // Settings survive a restart
        Assert.Equal(2, Service().StatPointsForLevel(4));

        // Setting every level clears the per-level amounts
        await settings.SetDefaultStatPointsAsync(0);
        Assert.Equal(0, settings.StatPointsForLevel(4));
        Assert.Empty(settings.GetStatPointTable().PerLevel);
    }

    [Fact]
    public async Task StatPoints_PickUpHandEditsToTheFile()
    {
        var settings = Service();
        await settings.SetDefaultStatPointsAsync(2);
        Assert.Equal(2, settings.StatPointsForLevel(3));

        await File.WriteAllTextAsync(_path, """{ "StatPointsPerLevel": 3, "StatPointsAtLevel": {} }""");
        File.SetLastWriteTimeUtc(_path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(3, settings.StatPointsForLevel(3));
    }

    [Fact]
    public async Task StatPoints_KeepTheLastGoodSettings_WhileAHandEditIsHalfSaved()
    {
        var settings = Service();
        await settings.SetDefaultStatPointsAsync(2);
        Assert.Equal(2, settings.StatPointsForLevel(3));

        await File.WriteAllTextAsync(_path, """{ "StatPointsPerLevel": 3, "StatPoi""");
        File.SetLastWriteTimeUtc(_path, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(2, settings.StatPointsForLevel(3));
    }

    [Fact]
    public async Task Saving_ReplacesTheFileWhole_AndLeavesNothingBehind()
    {
        var settings = Service();
        await Task.WhenAll(Enumerable.Range(2, 10).Select(level => settings.SetStatPointsForLevelAsync(level, 3)));

        // Every save landed, none over another's
        Assert.Equal(10, settings.GetStatPointTable().PerLevel.Count);
        Assert.Equal(10, Service().GetStatPointTable().PerLevel.Count);
        Assert.False(File.Exists(_path + ".saving"));
    }

    [Fact]
    public async Task LevelingUp_UsesTheDmsAmounts()
    {
        var settings = Service();
        await settings.SetStatPointsForLevelAsync(3, 2);
        await settings.SetStatPointsForLevelAsync(4, 0);

        using var database = new TestDatabase();
        await using var context = database.CreateContext();

        var character = new Character
        {
            ID = new Snowflake(5),
            Name = "Mal",
            Level = 1,
            Class = new Class { Type = ClassType.Captain },
            Health = new Health { CurrentHP = 10, MaxHP = 10 },
            Inventory = new Inventory(),
            Stats = [],
        };
        var characters = new CharacterService(context);
        await characters.AddCharacterAsync(character);
        var leveling = new LevelingService(characters, new ResourceService(context), settings, new SubclassService(new AbilityService(context)));

        // Level 2 gives 1, level 3 gives 2, level 4 gives 0
        await leveling.ChangeLevelAsync(character, 4);

        Assert.Equal(3, leveling.GetStatPoints(character));
    }
}

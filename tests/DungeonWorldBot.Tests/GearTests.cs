using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Gear;
using DungeonWorldBot.Gear.Homebrew;
using DungeonWorldBot.Rolling;
using DungeonWorldBot.Services.Implementation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests;

public sealed class GearTests : IDisposable
{
    private static readonly Snowflake UserId = new(66);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public GearTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private DungeonWorldContext CreateContext() =>
        new(new DbContextOptionsBuilder<DungeonWorldContext>().UseSqlite(_connection).Options);

    private static GearMode Gem(string name) => GemAspectedBow.Gear.FindMode(name)!;

    private async Task<(Character, GearService, ResourceService)> SetupAsync(DungeonWorldContext context, IDiceRoller? dice = null)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Selene",
            Class = new Class { Type = ClassType.Ranger },
            Health = new Health { CurrentHP = 20, MaxHP = 20 },
            Inventory = new Inventory(),
            Stats = [],
            Abilities = [],
        };
        await new CharacterService(context).AddCharacterAsync(character);

        var resources = new ResourceService(context);
        var gear = new GearService(new AbilityService(context), resources, dice ?? new DiceRoller());
        await gear.GiveAsync(character, GemAspectedBow.Gear);
        return (character, gear, resources);
    }

    [Fact]
    public void Gear_FitsInDiscord()
    {
        Assert.All(GearLibrary.All.SelectMany(g => g.Modes), m =>
        {
            Assert.InRange($"{m.Name}: {m.Title}".Length, 1, 256);
            Assert.InRange($"{m.Effect}\n*{m.Visual}*".Length, 1, 1024);
        });
        Assert.Equal(6, GemAspectedBow.Gear.Modes.Count);
    }

    [Fact]
    public async Task Socketing_IsSavedAndReadBack()
    {
        await using (var context = CreateContext())
        {
            var (character, gear, _) = await SetupAsync(context);
            Assert.Null(Assert.Single(GearRules.Owned(character)).Mode);

            await gear.SetModeAsync(character, GemAspectedBow.Gear, Gem("ruby"));
        }

        await using (var context = CreateContext())
        {
            var character = (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!;
            Assert.Equal("Ruby", Assert.Single(GearRules.Owned(character)).Mode?.Name);
        }
    }

    [Fact]
    public async Task Ruby_GainsDodgesOnDamage_UpToThree_AndLosesThemWhenSwapped()
    {
        await using var context = CreateContext();
        var (character, gear, _) = await SetupAsync(context);
        var dodge = Gem("Ruby").Counter!;
        await gear.SetModeAsync(character, GemAspectedBow.Gear, Gem("Ruby"));

        for (var i = 0; i < 4; i++)
            await gear.ApplyToDamageAsync(character, "Volley", [(8, 4)]);
        Assert.Equal(3, gear.GetCounter(character, dodge));

        // A sword swing isn't the bow
        await gear.SetCounterAsync(character, dodge, 1);
        await gear.ApplyToDamageAsync(character, "Hack and Slash", [(8, 4)]);
        Assert.Equal(1, gear.GetCounter(character, dodge));

        await gear.SetModeAsync(character, GemAspectedBow.Gear, Gem("Diamond"));
        Assert.Equal(0, gear.GetCounter(character, dodge));
    }

    [Fact]
    public async Task Sapphire_RerollsTheLowestDie_KeepingTheHigher()
    {
        await using var context = CreateContext();
        var (character, gear, _) = await SetupAsync(context, new DiceRoller(new d20Tek.DiceNotation.DieRoller.ConstantDieRoller(7)));
        await gear.SetModeAsync(character, GemAspectedBow.Gear, Gem("Sapphire"));

        var result = await gear.ApplyToDamageAsync(character, "Volley", [(8, 2), (6, 5)]);
        Assert.Equal(5, result.Bonus);
        Assert.Contains("d8, 2 → 7", Assert.Single(result.Notes));

        Assert.Equal(new Reroll(8, 6, 3), GearRules.RerollLowest([(8, 6)], _ => 3));
        Assert.Equal(0, new Reroll(8, 6, 3).Gain);
        Assert.Null(GearRules.RerollLowest([], _ => 3));
    }

    [Fact]
    public async Task NotesOnlyAppearWithAGemSocketed()
    {
        await using var context = CreateContext();
        var (character, gear, _) = await SetupAsync(context);

        Assert.Empty((await gear.ApplyToDamageAsync(character, "Volley", [(8, 4)])).Notes);

        await gear.SetModeAsync(character, GemAspectedBow.Gear, Gem("Diamond"));
        Assert.Contains("armor", Assert.Single((await gear.ApplyToDamageAsync(character, "Volley", [(8, 4)])).Notes));
    }

    [Fact]
    public void DamageDice_ReadsEachDiesSides()
    {
        var roll = new DiceRoller(new d20Tek.DiceNotation.DieRoller.ConstantDieRoller(4)).Roll("2d8+1d6+3");

        Assert.Equal([(8, 4), (8, 4), (6, 4)], DamageService.DamageDice(roll));
    }

    [Theory]
    [InlineData(7, 4)]
    [InlineData(8, 4)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void OpportunistsVolley_HalvesRoundingUp(int damage, int dealt)
    {
        Assert.Equal(dealt, GearRules.Half(damage));
        Assert.True(Gem("Topaz").Action!.HalfDamage);
    }
}

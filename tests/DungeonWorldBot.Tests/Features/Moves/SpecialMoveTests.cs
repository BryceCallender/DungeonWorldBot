using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Debilities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Rules;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Moves;

public sealed class SpecialMoveTests : IDisposable
{
    private static readonly Snowflake UserId = new(99);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    public static IEnumerable<object[]> AllMoves() => SpecialMoves.All.Select(m => new object[] { m });

    private static Character NewCharacter(int currentHP, params Item[] items) => new()
    {
        ID = UserId,
        Name = "Ren Sow",
        Class = new Class { Type = ClassType.Slayer },
        Health = new Health { CurrentHP = currentHP, MaxHP = 21 },
        Inventory = new Inventory { Items = items.ToList() },
        Stats = [],
    };

    private static SpecialMoveService NewService(DungeonWorldContext context) => new(context, new DebilityService(context));

    [Theory]
    [MemberData(nameof(AllMoves))]
    public void EveryMove_HasTextForEveryOutcome(MoveDefinition move)
    {
        Assert.All(Enum.GetValues<MoveOutcome>(), outcome => Assert.False(string.IsNullOrWhiteSpace(move.Describe(outcome)), $"{move.Name}: {outcome}"));
        Assert.False(string.IsNullOrWhiteSpace(move.Trigger));
    }

    [Fact]
    public void EveryJourneyRole_HasItsOwnMove()
    {
        Assert.All(Enum.GetValues<JourneyRole>(), role => Assert.EndsWith(role.ToString(), SpecialMoves.PerilousJourney(role).Name));
    }

    [Fact]
    public void Carouse_ShowsOptionsEvenOnAMiss()
    {
        Assert.All(Enum.GetValues<MoveOutcome>(), outcome => Assert.True(SpecialMoves.Carouse.ShowsOptions(outcome)));
    }

    [Theory]
    [InlineData(1, false, 0)]
    [InlineData(3, false, 1)]
    [InlineData(7, false, 2)]
    [InlineData(2, true, 1)]
    [InlineData(5, true, 2)]
    public void Recover_ClearsADebilityEveryFewDays(int days, bool healer, int expected)
    {
        Assert.Equal(expected, SpecialMoveRules.DebilitiesRecovered(days, healer));
    }

    [Fact]
    public async Task MakeCamp_EatsARationAndHealsHalfMaxHP()
    {
        await using (var context = CreateContext())
        {
            var character = NewCharacter(4, new Item { Name = "Dungeon Rations", Amount = 2 }, new Item { Name = "Coins", Amount = 5 });
            await new CharacterService(context).AddCharacterAsync(character);

            var camp = await NewService(context).MakeCampAsync(character, slept: true);

            Assert.Equal(new CampResult("Dungeon Rations", 1, 10), camp);
            Assert.Equal(14, character.Health.CurrentHP);
        }

        await using (var context = CreateContext())
        {
            var character = (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!;
            Assert.Equal(1, SpecialMoveRules.Rations(character)!.Amount);

            // Eating the last one removes it, and healing stops at max HP
            var camp = await NewService(context).MakeCampAsync(character, slept: true);

            Assert.Equal(new CampResult("Dungeon Rations", 0, 7), camp);
            Assert.Null(SpecialMoveRules.Rations(character));
            Assert.Equal(21, character.Health.CurrentHP);
        }
    }

    [Fact]
    public async Task MakeCamp_WithoutRationsOrSleep_ChangesNothing()
    {
        await using var context = CreateContext();
        var character = NewCharacter(4);
        await new CharacterService(context).AddCharacterAsync(character);

        var camp = await NewService(context).MakeCampAsync(character, slept: false);

        Assert.Equal(new CampResult(null, 0, 0), camp);
        Assert.Equal(4, character.Health.CurrentHP);
    }

    [Fact]
    public async Task Recover_HealsFullyAndClearsEarnedDebilities()
    {
        await using var context = CreateContext();
        var character = NewCharacter(3);
        await new CharacterService(context).AddCharacterAsync(character);
        var debilities = new DebilityService(context);
        await debilities.AddAsync(character, DebilityType.Weak);
        await debilities.AddAsync(character, DebilityType.Sick);
        var service = NewService(context);

        // Earned one but has two, and didn't pick
        var unpicked = await service.RecoverAsync(character, days: 3, healer: false, chosen: null);
        Assert.Equal(18, unpicked.Healed);
        Assert.Empty(unpicked.Removed);
        Assert.Equal(1, unpicked.StillToRemove);
        Assert.Equal(21, character.Health.CurrentHP);

        var picked = await service.RecoverAsync(character, days: 3, healer: false, chosen: DebilityType.Sick);
        Assert.Equal([DebilityType.Sick], picked.Removed);
        Assert.Equal(0, picked.StillToRemove);
        Assert.Equal([DebilityType.Weak], DebilityRules.Current(character));

        await debilities.AddAsync(character, DebilityType.Shaky);
        var all = await service.RecoverAsync(character, days: 4, healer: true, chosen: null);
        Assert.Equal([DebilityType.Weak, DebilityType.Shaky], all.Removed);
        Assert.Empty(DebilityRules.Current(character));
    }

    [Theory]
    [InlineData(BolsterLength.Week, 1)]
    [InlineData(BolsterLength.Month, 3)]
    public void Bolster_TakesPreparationByHowLongTheyPrepared(BolsterLength length, int expected)
    {
        Assert.Equal(expected, SpecialMoveRules.PreparationFor(length));
    }

    [Theory]
    [InlineData(ClassType.Slayer)]
    [InlineData(ClassType.Wizard)]
    [InlineData(ClassType.Battlemaster)]
    public void EveryCharacter_HasPreparation_WhichResettingAllLeavesAlone(ClassType type)
    {
        var character = NewCharacter(10);
        character.Class.Type = type;

        Assert.Contains(SpecialMoveRules.Preparation, ClassSheetLibrary.ResourcesOf(character));
        Assert.False(SpecialMoveRules.Preparation.ResetWithAll);
    }

    [Fact]
    public async Task Preparation_SavesLikeAnyResource()
    {
        await using (var context = CreateContext())
        {
            var character = NewCharacter(10);
            await new CharacterService(context).AddCharacterAsync(character);
            var resources = new ResourceService(context);

            Assert.Equal(0, resources.GetValue(character, SpecialMoveRules.Preparation));
            Assert.Equal(3, await resources.SetValueAsync(character, SpecialMoveRules.Preparation, 3));
        }

        await using (var context = CreateContext())
        {
            var character = (await new CharacterService(context).GetCharacterFromUserAsync(UserId))!;
            Assert.Equal(3, new ResourceService(context).GetValue(character, SpecialMoveRules.Preparation));
        }
    }

    [Fact]
    public async Task SpendCoins_OnlyWhenTheyHaveEnough()
    {
        await using var context = CreateContext();
        var character = NewCharacter(10, new Item { Name = "Coins", Amount = 250 });
        await new CharacterService(context).AddCharacterAsync(character);
        var service = NewService(context);

        Assert.False(await service.SpendCoinsAsync(character, 300));
        Assert.Equal(250, SpecialMoveRules.Coins(character)!.Amount);

        Assert.True(await service.SpendCoinsAsync(character, 200));
        Assert.Equal(50, SpecialMoveRules.Coins(character)!.Amount);

        Assert.True(await service.SpendCoinsAsync(character, 50));
        Assert.Null(SpecialMoveRules.Coins(character));
    }
}

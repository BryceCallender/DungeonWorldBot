using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Shapeshifting;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Shapeshifting;

public sealed class TransformationTests : IDisposable
{
    private static readonly Snowflake UserId = new(88);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static async Task<Character> AddDruidAsync(DungeonWorldContext context, params string[] moves)
    {
        var character = new Character
        {
            ID = UserId,
            Name = "Fern",
            Class = new Class { Type = ClassType.Druid },
            Health = new Health { CurrentHP = 16, MaxHP = 16 },
            Inventory = new Inventory(),
            Stats = [],
            Abilities = moves.Select(m => new Ability { Name = m }).ToList(),
        };
        await new CharacterService(context).AddCharacterAsync(character);
        return character;
    }

    private static TransformationService ServiceFor(DungeonWorldContext context) =>
        new(context, new ResourceService(context));

    [Fact]
    public async Task NewDruid_KnowsNoForms()
    {
        await using var context = CreateContext();
        var character = await AddDruidAsync(context, ShapeshiftRules.Shapeshifter);

        Assert.True(ShapeshiftRules.CanShapeshift(character));
        Assert.Empty(character.Transformations ?? []);
        Assert.Equal("Natural form", ShapeshiftRules.Status(character, "0"));
    }

    [Theory]
    [InlineData(MoveOutcome.StrongHit, 3)]
    [InlineData(MoveOutcome.WeakHit, 2)]
    [InlineData(MoveOutcome.Miss, 1)]
    public void Hold_FollowsTheRoll(MoveOutcome outcome, int hold) =>
        Assert.Equal(hold, ShapeshiftRules.HoldFor(outcome));

    [Fact]
    public async Task LearnedForms_AreSavedAndFoundByName()
    {
        await using var context = CreateContext();
        var character = await AddDruidAsync(context, ShapeshiftRules.Shapeshifter);
        var service = ServiceFor(context);

        await service.LearnAsync(character, " Bear ", "Big, strong, poor eyesight", null);
        await service.LearnAsync(character, "Hawk", null, "Fly; Spot prey from afar");

        await using var reloaded = CreateContext();
        var saved = await new CharacterService(reloaded).GetCharacterFromUserAsync(UserId);

        Assert.Equal(["Bear", "Hawk"], saved!.Transformations!.Select(t => t.Name).Order());
        Assert.Equal("Fly; Spot prey from afar", ShapeshiftRules.Find(saved, "hawk")!.Moves);
    }

    [Fact]
    public async Task Shifting_SetsTheFormAndHold_AndRevertingClearsThem()
    {
        await using var context = CreateContext();
        var character = await AddDruidAsync(context, ShapeshiftRules.Shapeshifter);
        var service = ServiceFor(context);
        var resources = new ResourceService(context);
        var bear = await service.LearnAsync(character, "Bear", null, null);
        var hawk = await service.LearnAsync(character, "Hawk", null, null);

        await service.ShiftAsync(character, [bear], 3);
        await service.ShiftAsync(character, [hawk], 2);

        Assert.Equal("Hawk", ShapeshiftRules.FormName(character));
        Assert.Equal(2, resources.GetValue(character, ShapeshiftRules.Hold));
        Assert.Equal(1, hawk.TimesTaken);

        var left = await service.RevertAsync(character);

        Assert.Equal([hawk], left);
        Assert.Null(ShapeshiftRules.FormName(character));
        Assert.Equal(0, resources.GetValue(character, ShapeshiftRules.Hold));
    }

    [Fact]
    public async Task Chimera_AllowsMergingThreeShapes()
    {
        await using var context = CreateContext();
        var druid = await AddDruidAsync(context, ShapeshiftRules.Shapeshifter);
        Assert.Equal(1, ShapeshiftRules.MaxShapes(druid));

        druid.Abilities!.Add(new Ability { Name = ShapeshiftRules.Chimera });
        Assert.Equal(3, ShapeshiftRules.MaxShapes(druid));
    }
}

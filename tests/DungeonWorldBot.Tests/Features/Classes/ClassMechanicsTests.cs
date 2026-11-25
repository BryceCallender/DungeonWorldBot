using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Tests.Features.Classes;

public class ClassMechanicsTests
{
    private static Character Of(ClassType type, params string[] moves) => new()
    {
        Name = "Fern",
        Class = new Class { Type = type },
        Abilities = moves.Select(m => new Ability { Name = m }).ToList(),
    };

    [Fact]
    public void Shapeshifting_IsForThoseWhoKnowShapeshifter()
    {
        Assert.True(Of(ClassType.Druid, ShapeshiftRules.Shapeshifter).Has(ClassMechanic.Shapeshifting));
        Assert.False(Of(ClassType.Druid).Has(ClassMechanic.Shapeshifting));
    }

    [Fact]
    public void Light_IsTheLanterns()
    {
        Assert.True(Of(ClassType.Lantern).Has(ClassMechanic.Light));
        Assert.False(Of(ClassType.Druid).Has(ClassMechanic.Light));
    }

    [Fact]
    public void AnotherClassesMechanic_IsntTheirs()
    {
        var druid = Of(ClassType.Druid, ShapeshiftRules.Shapeshifter);

        Assert.All(Enum.GetValues<ClassMechanic>().Where(m => m != ClassMechanic.Shapeshifting),
            m => Assert.False(druid.Has(m), $"A druid shouldn't have {m}"));
    }
}

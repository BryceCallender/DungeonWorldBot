using DungeonWorldBot.ClassSheets;
using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Tests;

public class MulticlassTests
{
    private static ClassSheet Sheet(ClassType type) => ClassSheetLibrary.For(type)!;

    private static Character Character(ClassType type, int level, params string[] moves) => new()
    {
        Name = "Test",
        Level = level,
        Class = new Class { Type = type },
        Abilities = moves.Select((m, i) => new Ability { ID = i + 1, Name = m }).ToList(),
    };

    private static ForeignMove Foreign(ClassType own, string name) => Multiclassing.Find(name, Sheet(own)).Single();

    private static string? WhyNot(Character character, string move) =>
        Multiclassing.WhyCannotLearn(character, Sheet(character.Class.Type), Foreign(character.Class.Type, move));

    [Fact]
    public void SpecialTrick_TakesOneMoveFromAnyOtherClass()
    {
        var ranger = Character(ClassType.Ranger, 6, "Special Trick");

        Assert.Null(WhyNot(ranger, "Channel"));

        ranger.Abilities!.Add(new Ability { ID = 10, Name = "Channel" });
        Assert.Contains("already used", WhyNot(ranger, "Raise the Dead"));
    }

    [Fact]
    public void WithoutAGrant_OtherClassesMovesAreRefused()
    {
        Assert.Contains("You need a move like Special Trick", WhyNot(Character(ClassType.Ranger, 6), "Channel"));
    }

    [Fact]
    public void ClassLimitedGrants_OnlyAllowThatClass()
    {
        var templar = Character(ClassType.Templar, 3, "White Knight");

        Assert.Null(WhyNot(templar, "Lay on Hands"));
        Assert.Contains("None of your moves", WhyNot(templar, "Hunt and Track"));
    }

    [Fact]
    public void Dabblers_CountAsOneLevelLower()
    {
        Assert.Contains("None of your moves", WhyNot(Character(ClassType.Captain, 6, "Discerning Dabbler"), "Lethal Gambit"));
        Assert.Null(WhyNot(Character(ClassType.Captain, 7, "Discerning Dabbler"), "Lethal Gambit"));
    }

    [Fact]
    public void Exclusions_AndOnlyMoves_AreRespected()
    {
        Assert.Contains("None of your moves", WhyNot(Character(ClassType.Battlemaster, 3, "Multiclass Learner"), "Called Shot"));
        Assert.Null(WhyNot(Character(ClassType.Battlemaster, 6, "Surprise Attack"), "Called Shot"));
        Assert.Contains("None of your moves", WhyNot(Character(ClassType.Battlemaster, 6, "Surprise Attack"), "Hunt and Track"));
    }

    [Fact]
    public void GrantsArePairedWithMovesTheyCanCover()
    {
        // Called Shot can only come from Surprise Attack, which leaves Multiclass Learner free for something else
        var battlemaster = Character(ClassType.Battlemaster, 6, "Multiclass Learner", "Surprise Attack", "Called Shot");

        Assert.Null(WhyNot(battlemaster, "Hunt and Track"));
    }

    [Fact]
    public void MovesThatGrantOtherMoves_CantBeTaken()
    {
        Assert.Empty(Multiclassing.Find("Special Trick", Sheet(ClassType.Battlemaster)));
        Assert.Empty(Multiclassing.Find("Mighty Dabbler", Sheet(ClassType.Ranger)));
    }

    [Fact]
    public void ForeignMoveRequirements_StillApply()
    {
        Assert.Contains("requires Thread the Needle", WhyNot(Character(ClassType.Ranger, 7, "Special Trick"), "Fissure"));
    }

    [Fact]
    public void SharedNames_CanBeQualifiedWithTheirClass()
    {
        var ranger = Sheet(ClassType.Ranger);

        Assert.Equal(2, Multiclassing.Find("Protector", ranger).Count);
        Assert.Equal(ClassType.Shaman, Multiclassing.Find("Protector (Shaman)", ranger).Single().Sheet.Type);
        Assert.Equal(ClassType.SkyDancer, Multiclassing.Find("Rocket Man (Sky Dancer)", ranger).Single().Sheet.Type);
    }

    [Fact]
    public void KnownForeignMoves_UseTheSourceNoteForSharedNames()
    {
        var ranger = Character(ClassType.Ranger, 7, "Special Trick");
        ranger.Abilities!.Add(new Ability { ID = 5, Name = "Protector", Description = "...\n" + Multiclassing.SourceNote(Sheet(ClassType.Shaman)) });

        var known = Multiclassing.KnownForeignMoves(ranger, Sheet(ClassType.Ranger));

        Assert.Equal(ClassType.Shaman, Assert.Single(known).Sheet.Type);
    }
}

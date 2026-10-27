using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.Guides;

namespace DungeonWorldBot.Tests.Features.Classes;

public class ClassGuideTests
{
    public static TheoryData<ClassType> SheetClasses() => new(ClassSheetLibrary.All.Select(s => s.Type));

    private static ClassSheet Sheet(ClassType type) => ClassSheetLibrary.For(type)!;

    private static IEnumerable<GuideCommand> Commands(ClassType type) =>
        ClassGuides.For(Sheet(type))!.Steps.SelectMany(s => s.Commands);

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void EveryClassHasAGuideInTheSameOrder(ClassType type)
    {
        var guide = ClassGuides.For(Sheet(type));

        Assert.NotNull(guide);
        Assert.Equal(type, guide.Class);
        Assert.Equal(["Make your character", "Basic moves", "Class moves"], guide.Steps.Take(3).Select(s => s.Title));
        Assert.True(guide.Steps.Count > 4, "Each class has at least one step of its own");
        Assert.Equal("Level up", guide.Steps[^1].Title);
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void ClassMovesStepListsEveryRolledStartingMove(ClassType type)
    {
        var sheet = Sheet(type);
        var step = ClassGuides.For(sheet)!.Steps[2];

        Assert.Equal(
            ClassGuides.RolledMoves(sheet).Select(m => m.Name),
            step.Commands.Where(c => c.Command == "/move class").Select(c => c.Pick));
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void ClassMovesInTheGuideAreRolledMovesOnTheSheet(ClassType type)
    {
        var sheet = Sheet(type);
        var missing = Commands(type)
            .Where(c => c is { Command: "/move class", Pick: not null })
            .Where(c => sheet.FindMove(c.Pick!)?.Roll is null)
            .Select(c => c.Pick);

        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void ResourcesInTheGuideAreOnTheSheet(ClassType type)
    {
        var sheet = Sheet(type);
        var missing = Commands(type)
            .Where(c => c.Command.StartsWith("/resource ") && c.Pick is not null)
            .Where(c => sheet.FindResource(c.Pick!) is null)
            .Select(c => c.Pick);

        Assert.Empty(missing);
    }
}

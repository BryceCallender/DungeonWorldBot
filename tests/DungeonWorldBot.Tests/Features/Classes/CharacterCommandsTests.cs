using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.Guides;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Features.Subclasses.Homebrew;
using DungeonWorldBot.Rules;
using DungeonWorldBot.Tests.Infrastructure;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Classes;

public class CharacterCommandsTests
{
    public static TheoryData<ClassType> SheetClasses() => new(ClassSheetLibrary.All.Select(s => s.Type));

    public static TheoryData<string> Subclasses() => new(SubclassLibrary.All.Select(s => s.Name));

    private static Character NewCharacter(ClassType type, int level = 1, params string[] abilities) => new()
    {
        ID = new Snowflake(1),
        Name = "Vesk",
        Level = level,
        Class = new Class { Type = type },
        Health = new Health { CurrentHP = 20, MaxHP = 20 },
        Inventory = new Inventory(),
        Stats = [],
        Abilities = abilities.Select(a => new Ability { Name = a }).ToList(),
    };

    private static IEnumerable<string> Commands(Character character) =>
        CharacterCommands.For(character).SelectMany(s => s.Commands).Select(c => c.Command);

    private static void AssertAllInTree(Character character)
    {
        var paths = CommandTreeTests.Paths(CommandTreeTests.BuildTree().Root).ToHashSet();
        Assert.Empty(Commands(character).Distinct().Where(c => !paths.Contains(c)));
    }

    [Theory]
    [MemberData(nameof(SheetClasses))]
    public void EveryCommandForAClassIsInTheCommandTree(ClassType type)
    {
        var sheet = ClassSheetLibrary.For(type)!;
        var everything = sheet.AllMoves.Select(m => m.Name).Concat(GearLibrary.All.Select(g => g.Name)).ToArray();

        AssertAllInTree(NewCharacter(type, 10, everything));
    }

    [Theory]
    [MemberData(nameof(Subclasses))]
    public void EveryCommandForASubclassIsInTheCommandTree(string name)
    {
        var subclass = SubclassLibrary.Find(name)!;
        var type = subclass.Classes.FirstOrDefault(ClassType.Fighter);

        AssertAllInTree(NewCharacter(type, 10, [subclass.Name, .. subclass.Moves.Select(m => m.Move.Name)]));
    }

    [Fact]
    public void SubclassCommandsOnlyShowOnceTheirMoveIsUnlocked()
    {
        var commands = Commands(NewCharacter(ClassType.Ranger, 1, ChampionOfLuna.Name, ChampionOfLuna.LunasVow)).ToList();

        Assert.Contains("/luna vow", commands);
        Assert.DoesNotContain("/luna blood-moon", commands);
    }

    [Fact]
    public void ClassMechanicsOnlyShowForWhoHasThem()
    {
        var battlemaster = Commands(NewCharacter(ClassType.Battlemaster, 1, TacticRules.Tactics)).ToList();
        var ranger = Commands(NewCharacter(ClassType.Ranger)).ToList();

        Assert.Contains("/tactic show", battlemaster);
        Assert.DoesNotContain("/tactic show", ranger);
        Assert.DoesNotContain("/shapeshift into", ranger);
        Assert.DoesNotContain(ranger, c => c.StartsWith("/luna"));
    }

    [Fact]
    public void DruidsGetShapeshiftingButNotItsMoveInClassMoves()
    {
        var steps = CharacterCommands.For(NewCharacter(ClassType.Druid, 1, ShapeshiftRules.Shapeshifter));

        Assert.Contains(steps, s => s.Commands.Any(c => c.Command == "/shapeshift into"));
        Assert.DoesNotContain(steps.SelectMany(s => s.Commands), c => c is { Command: "/move class", Pick: ShapeshiftRules.Shapeshifter });
    }
}

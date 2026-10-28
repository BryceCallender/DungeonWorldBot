using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Features.Subclasses.Homebrew;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Subclasses;

public sealed class SubclassTests : IDisposable
{
    private static readonly Snowflake UserId = new(55);

    private readonly TestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private DungeonWorldContext CreateContext() => _db.CreateContext();

    private static Character NewCharacter(ulong id, string name, int level = 1, params Ability[] abilities) => new()
    {
        ID = new Snowflake(id),
        Name = name,
        Level = level,
        Class = new Class { Type = ClassType.Ranger },
        Health = new Health { CurrentHP = 20, MaxHP = 20 },
        Inventory = new Inventory(),
        Stats = [],
        Abilities = abilities.ToList(),
    };

    private static Ability Vow(LunaBoon boon, StatType? stat = null) =>
        new() { Name = ChampionOfLuna.ActiveVow, Description = ChampionOfLuna.Store(new LunaVow(boon, stat, "Slay the wyrm")) };

    private static Character Champion(params Ability[] extra) =>
        NewCharacter(1, "Selene", 3, [new Ability { Name = ChampionOfLuna.Name }, .. extra]);

    [Fact]
    public void Subclasses_FitInDiscord()
    {
        Assert.All(SubclassLibrary.All.SelectMany(s => s.Moves), m =>
        {
            Assert.InRange(m.Move.Name.Length, 1, 200);
            Assert.InRange(m.Move.Summary.Length, 1, 1024);
        });
        Assert.Equal(SubclassLibrary.All.Count, SubclassLibrary.All.DistinctBy(s => s.Name).Count());
    }

    [Fact]
    public void StormCaller_IsForAnyClassAndRollsCon()
    {
        Assert.True(StormCaller.Subclass.AllowedFor(ClassType.Ranger));
        Assert.True(StormCaller.Subclass.AllowedFor(ClassType.Paladin));
        Assert.Same(StormCaller.Subclass, SubclassLibrary.Find("storm caller"));
        Assert.Equal([StormCaller.SummonTheStorm, StormCaller.ChannelTheStorm], StormCaller.Subclass.MovesAt(1).Select(m => m.Name));
        Assert.All(StormCaller.Subclass.Moves, m => Assert.Equal(StatType.Con, m.Move.Roll?.Stat));
    }

    [Fact]
    public void CircleOfTheEclipse_IsForDruidsAndRollsWis()
    {
        Assert.True(CircleOfTheEclipse.Subclass.AllowedFor(ClassType.Druid));
        Assert.False(CircleOfTheEclipse.Subclass.AllowedFor(ClassType.Ranger));
        Assert.Same(CircleOfTheEclipse.Subclass, SubclassLibrary.Find("circle of the eclipse"));
        Assert.Equal(
            [CircleOfTheEclipse.AspectShift, CircleOfTheEclipse.EclipseTouched, CircleOfTheEclipse.SolarMend, CircleOfTheEclipse.PhaseDrift],
            CircleOfTheEclipse.Subclass.MovesAt(1).Select(m => m.Name));
        Assert.Equal(8, CircleOfTheEclipse.Subclass.MovesAt(9).Count());
        Assert.Equal(StatType.Wis, CircleOfTheEclipse.Subclass.FindMove(CircleOfTheEclipse.BlindingRadiance)!.Roll?.Stat);
    }

    [Fact]
    public void CircleOfTheEclipse_TracksAspect()
    {
        var druid = NewCharacter(1, "Selene", 3, [new Ability { Name = CircleOfTheEclipse.Name }]);
        Assert.Null(CircleOfTheEclipse.AspectOf(druid));
        Assert.Contains("/eclipse aspect", CircleOfTheEclipse.Subclass.Status!(druid));

        druid.Abilities!.Add(new Ability { Name = CircleOfTheEclipse.CurrentAspect, Description = "Moonward" });
        Assert.Equal(EclipseAspect.Moonward, CircleOfTheEclipse.AspectOf(druid));
        Assert.Contains("Moonward", CircleOfTheEclipse.Subclass.Status!(druid));
        Assert.Contains(CircleOfTheEclipse.CurrentAspect, CircleOfTheEclipse.Subclass.StateAbilities);

        Assert.Equal([CircleOfTheEclipse.SolarMend], CircleOfTheEclipse.AspectMoves(EclipseAspect.Sunward, 2));
        Assert.Equal(
            [CircleOfTheEclipse.PhaseDrift, CircleOfTheEclipse.ShadowStep, "Lunar Balance"],
            CircleOfTheEclipse.AspectMoves(EclipseAspect.Moonward, 6));
    }

    [Fact]
    public void ChampionOfLuna_IsForRangers()
    {
        Assert.True(ChampionOfLuna.Subclass.AllowedFor(ClassType.Ranger));
        Assert.False(ChampionOfLuna.Subclass.AllowedFor(ClassType.Paladin));
        Assert.Same(ChampionOfLuna.Subclass, SubclassLibrary.Find("champion of luna"));
    }

    [Fact]
    public async Task Joining_GivesMovesForLevel_AndLevellingUnlocksMore()
    {
        await using var context = CreateContext();
        var characters = new CharacterService(context);
        var character = NewCharacter(UserId.Value, "Selene", level: 2);
        await characters.AddCharacterAsync(character);

        var subclasses = new SubclassService(new AbilityService(context));
        var learned = await subclasses.JoinAsync(character, ChampionOfLuna.Subclass);

        Assert.Equal([ChampionOfLuna.BloodMoon, ChampionOfLuna.LunasVow], learned.Select(m => m.Name));
        Assert.Same(ChampionOfLuna.Subclass, SubclassLibrary.Of(character));

        var leveling = new LevelingService(characters, new ResourceService(context), subclasses: subclasses);
        await leveling.ChangeLevelAsync(character, 10);

        var known = ClassSheetLibrary.KnownMoves(character);
        Assert.Contains(ChampionOfLuna.Revelation, known);
        Assert.Contains(ChampionOfLuna.LunarBarrage, known);
        Assert.Contains(ChampionOfLuna.LifeForLife, known);

        // The level 10 secret is never handed out
        Assert.DoesNotContain("???", known);

        await subclasses.LeaveAsync(character, ChampionOfLuna.Subclass);
        Assert.Null(SubclassLibrary.Of(character));
        Assert.Empty(ClassSheetLibrary.KnownMoves(character));
    }

    [Fact]
    public void Revelation_IsRolledWithWis_AndAsksEvenOnAMiss()
    {
        var revelation = ChampionOfLuna.Subclass.FindMove(ChampionOfLuna.Revelation)!;

        Assert.Equal(StatType.Wis, revelation.Roll!.Stat);
        Assert.True(revelation.Roll.ShowsOptions(MoveOutcome.Miss));
        Assert.Null(ChampionOfLuna.Subclass.FindMove("???"));
    }

    [Fact]
    public void Vow_RoundTripsThroughItsAbility()
    {
        var character = Champion(Vow(LunaBoon.Aptitude, StatType.Dex));

        Assert.Equal(new LunaVow(LunaBoon.Aptitude, StatType.Dex, "Slay the wyrm"), ChampionOfLuna.ActiveVowOf(character));
        Assert.Null(ChampionOfLuna.ActiveVowOf(Champion()));
    }

    [Fact]
    public void Boons_GiveTheChampionFullPower_AndThePartyTheEcho()
    {
        var champion = Champion(Vow(LunaBoon.Wrath));
        var ally = NewCharacter(2, "Bram");
        var party = new[] { champion, ally };

        Assert.Equal(6, Assert.Single(SubclassLibrary.Bonuses(champion, party, RollKind.Damage)).Amount);
        var echo = Assert.Single(SubclassLibrary.Bonuses(ally, party, RollKind.Damage));
        Assert.Equal(3, echo.Amount);
        Assert.Contains("Selene", echo.Source);

        Assert.Empty(SubclassLibrary.Bonuses(champion, party, RollKind.Stat, StatType.Str));
        Assert.Empty(SubclassLibrary.Bonuses(ally, party, RollKind.LastBreath));
    }

    [Fact]
    public void BoonOfAptitude_OnlyRaisesItsStat()
    {
        var champion = Champion(Vow(LunaBoon.Aptitude, StatType.Wis));
        var ally = NewCharacter(2, "Bram");
        var party = new[] { champion, ally };

        Assert.Equal(2, SubclassLibrary.Bonuses(champion, party, RollKind.Stat, StatType.Wis).Single().Amount);
        Assert.Equal(1, SubclassLibrary.Bonuses(ally, party, RollKind.Stat, StatType.Wis).Single().Amount);
        Assert.Empty(SubclassLibrary.Bonuses(ally, party, RollKind.Stat, StatType.Cha));
    }

    [Fact]
    public void BoonOfFortune_HelpsLastBreath()
    {
        var champion = Champion(Vow(LunaBoon.Fortune));
        var ally = NewCharacter(2, "Bram");

        Assert.Equal(2, SubclassLibrary.Bonuses(champion, [ally], RollKind.LastBreath).Single().Amount);
        Assert.Equal(1, SubclassLibrary.Bonuses(ally, [champion, ally], RollKind.LastBreath).Single().Amount);
    }

    [Theory]
    [InlineData(MoveOutcome.StrongHit, 7, 7)]
    [InlineData(MoveOutcome.WeakHit, 7, 3)]
    [InlineData(MoveOutcome.Miss, 7, 0)]
    public void BloodMoon_RecoversBySuccess(MoveOutcome outcome, int sacrificed, int recovered)
    {
        Assert.Equal(recovered, ChampionOfLuna.Recovered(sacrificed, outcome));
    }

    [Fact]
    public void LunarBarrage_KeepsRollingOnSixOrMore()
    {
        var dice = new Queue<int>([10, 6, 8, 5, 9]);
        Assert.Equal([10, 6, 8, 5], ChampionOfLuna.RollBarrage(dice.Dequeue));

        Assert.Equal([3], ChampionOfLuna.RollBarrage(() => 3));
        Assert.Equal(100, ChampionOfLuna.RollBarrage(() => 10).Count);

        Assert.Equal(3, ChampionOfLuna.BarrageTargets(MoveOutcome.StrongHit));
        Assert.Equal(1, ChampionOfLuna.BarrageTargets(MoveOutcome.Miss));
    }

    [Fact]
    public void TacticalGuardian_IsForAnyClass_AndKeepsPoolMovesOffMoveClass()
    {
        Assert.True(TacticalGuardian.Subclass.AllowedFor(ClassType.Ranger));
        Assert.True(TacticalGuardian.Subclass.AllowedFor(ClassType.Paladin));
        Assert.Same(TacticalGuardian.Subclass, SubclassLibrary.Find("tactical guardian"));

        // Read the Room and Perfected Assault are rolled with /guardian so the pool and the short-rest use are tracked
        Assert.Null(TacticalGuardian.Subclass.FindMove(TacticalGuardian.ReadTheRoom)!.Roll);
        Assert.Null(TacticalGuardian.Subclass.FindMove(TacticalGuardian.PerfectedAssault)!.Roll);
        Assert.Equal(1, TacticalGuardian.Subclass.FindMove(TacticalGuardian.PerfectedAssault)!.Uses);
        Assert.Equal(StatType.Int, TacticalGuardian.ReadTheRoomRoll.Stat);
        Assert.Equal(StatType.Int, TacticalGuardian.Subclass.FindMove(TacticalGuardian.FlashstepExchange)!.Roll?.Stat);
        Assert.Equal(StatType.Con, TacticalGuardian.Subclass.FindMove(TacticalGuardian.GuardiansStand)!.Roll?.Stat);
    }

    [Fact]
    public void StrategyPool_RoundTripsThroughItsAbility_AndSpendsOneDie()
    {
        var pool = new[] { 4, 2, 6, 2 };
        var guardian = NewCharacter(1, "Aster", 1,
            new Ability { Name = TacticalGuardian.Name },
            new Ability { Name = TacticalGuardian.StrategyPool, Description = TacticalGuardian.StorePool(pool) });

        Assert.Equal(pool, TacticalGuardian.PoolOf(guardian));
        Assert.Equal([4, 6, 2], TacticalGuardian.Spend(pool, 2));
        Assert.Null(TacticalGuardian.Spend(pool, 5));
        Assert.Empty(TacticalGuardian.PoolOf(NewCharacter(2, "Bram")));
    }

    [Fact]
    public void StrategyPool_SizeFollowsTheRoll()
    {
        Assert.Equal(9, TacticalGuardian.PoolSize(9));
        Assert.Equal(0, TacticalGuardian.PoolSize(-1));
        Assert.Equal(TacticalGuardian.MaxPool, TacticalGuardian.PoolSize(99));
    }

    [Fact]
    public void ShieldedPursuit_StoresHalfOfTheDamageTaken()
    {
        Assert.Equal(3, TacticalGuardian.Stores(7));
        Assert.Equal(0, TacticalGuardian.Stores(1));

        // Taking an ally's hit with Guardian's Stand halves it first
        Assert.Equal(4, TacticalGuardian.DamageTaken(9, forAlly: true));
        Assert.Equal(9, TacticalGuardian.DamageTaken(9, forAlly: false));

        var guardian = NewCharacter(1, "Aster", 6, new Ability { Name = TacticalGuardian.StoredDamage, Description = "5" });
        Assert.Equal(5, TacticalGuardian.StoredDamageOf(guardian));
        Assert.Equal(0, TacticalGuardian.StoredDamageOf(NewCharacter(2, "Bram")));
    }
}

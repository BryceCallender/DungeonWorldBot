using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace DungeonWorldBot.Tests.Dashboard;

public sealed class EncounterDeskTests : IDisposable
{
    private static readonly SceneLink Ford = new("dEsXAr", "The ford", null);
    private static readonly EncounterRoster Nobody = new([], []);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero));
    private readonly DashboardTestServices _services;
    private readonly EncounterDesk _desk;

    public EncounterDeskTests()
    {
        _services = new DashboardTestServices(_time);
        _desk = new EncounterDesk(_services.Writes, _time);
    }

    public void Dispose() => _services.Dispose();

    private async Task SaveGoblinAsync() =>
        Assert.Null(await _desk.SaveMonsterAsync(null, new MonsterTemplate { Name = "Goblin", MaxHP = 4, Armor = 1, Damage = "d6" }));

    private async Task<Enemy> OneGoblinAsync()
    {
        await SaveGoblinAsync();
        var started = await _desk.StartAsync("Ambush", Ford, Nobody, [new MonsterCount("goblin", 1)]);
        return Assert.Single(started.Enemies);
    }

    [Fact]
    public async Task Start_SpawnsTheSavedMonstersIntoTheFight()
    {
        await SaveGoblinAsync();

        var started = await _desk.StartAsync("  Ambush ", Ford, Nobody, [new MonsterCount("goblin", 2)]);

        Assert.Null(started.Error);
        Assert.Equal(["Goblin 1", "Goblin 2"], started.Enemies.Select(e => e.Name));
        await using var context = _services.CreateContext();
        var fight = await context.Encounters.Include(e => e.Enemies).SingleAsync();
        Assert.Equal("Ambush", fight.Name);
        Assert.Equal(Ford.ID, fight.SceneID);
        Assert.Equal(2, fight.Enemies.Count);
    }

    [Fact]
    public async Task Start_WithAnUnknownMonster_LeavesNoFightBehind()
    {
        var started = await _desk.StartAsync("Ambush", Ford, Nobody, [new MonsterCount("Dragon", 1)]);

        Assert.Equal("There's no saved monster called Dragon.", started.Error);
        await using var context = _services.CreateContext();
        Assert.Empty(context.Encounters);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Start_NeedsAName(string name)
    {
        var started = await _desk.StartAsync(name, Ford, Nobody, []);

        Assert.StartsWith("Give the fight a name", started.Error);
    }

    [Fact]
    public async Task Start_RefusesTooManyOfAMonster()
    {
        var started = await _desk.StartAsync("Ambush", Ford, Nobody, [new MonsterCount("Goblin", EncounterDesk.MaxCount + 1)]);

        Assert.Equal($"Start with between 0 and {EncounterDesk.MaxCount} of Goblin.", started.Error);
    }

    [Fact]
    public async Task Start_OneFightPerScene_AndOneOfEachName()
    {
        Assert.Null((await _desk.StartAsync("Ambush", Ford, Nobody, [])).Error);

        var sameScene = await _desk.StartAsync("Raid", Ford, Nobody, []);
        var sameName = await _desk.StartAsync("ambush", new SceneLink("other", "Elsewhere", null), Nobody, []);

        Assert.Equal("Ambush is still going on here. End it first.", sameScene.Error);
        Assert.Equal("Ambush is already going on. Give this fight another name.", sameName.Error);
    }

    [Fact]
    public async Task AddMonster_NeedsAFightOnTheScene()
    {
        await SaveGoblinAsync();

        var added = await _desk.AddMonsterAsync(Ford.ID, "Goblin", 1);

        Assert.Equal("There's no fight going on here.", added.Error);
    }

    [Fact]
    public async Task AddCustom_CanSaveItAsAMonster_ButNotOverAnother()
    {
        await _desk.StartAsync("Ambush", Ford, Nobody, []);

        var ogre = await _desk.AddCustomAsync(Ford.ID, new MonsterTemplate { Name = "Ogre", MaxHP = 12, Armor = 2, Tags = "  Large " }, save: true);
        var again = await _desk.AddCustomAsync(Ford.ID, new MonsterTemplate { Name = "Troll", MaxHP = 12 }, save: true);
        var copy = await _desk.AddCustomAsync(Ford.ID, new MonsterTemplate { Name = "Ogre", MaxHP = 12 }, save: false);

        Assert.Null(ogre.Error);
        Assert.Null(again.Error);
        Assert.Equal("Ogre is already in a fight.", copy.Error);
        await using var context = _services.CreateContext();
        var saved = await context.MonsterTemplates.OrderBy(m => m.Name).ToListAsync();
        Assert.Equal(["Ogre", "Troll"], saved.Select(m => m.Name));
        Assert.Equal("Large", saved[0].Tags);
    }

    [Theory]
    [InlineData(0, 0, "Give it at least 1 HP.")]
    [InlineData(5, -1, "Armor can't be negative.")]
    public async Task SaveMonster_ChecksItsNumbers(int maxHP, int armor, string error)
    {
        Assert.Equal(error, await _desk.SaveMonsterAsync(null, new MonsterTemplate { Name = "Imp", MaxHP = maxHP, Armor = armor }));
    }

    [Fact]
    public async Task SaveMonster_ChangesOneAlreadySaved()
    {
        await SaveGoblinAsync();
        await using var context = _services.CreateContext();
        var goblin = await context.MonsterTemplates.SingleAsync();

        Assert.Null(await _desk.SaveMonsterAsync(goblin.ID, new MonsterTemplate { Name = "Goblin Chief", MaxHP = 8, Armor = 2 }));

        await context.Entry(goblin).ReloadAsync();
        Assert.Equal("Goblin Chief", goblin.Name);
        Assert.Equal(8, goblin.MaxHP);
        Assert.Equal("That monster isn't saved any more.", await _desk.SaveMonsterAsync(goblin.ID + 1, new MonsterTemplate { Name = "X", MaxHP = 1 }));
    }

    [Fact]
    public async Task Damage_IsReducedByArmor_AndHealingAddsBack()
    {
        var goblin = await OneGoblinAsync();

        Assert.Null(await _desk.ChangeEnemyHpAsync(goblin.ID, HpChange.Damage, 3));
        Assert.Equal(2, await CurrentHpAsync(goblin.ID));

        Assert.Null(await _desk.ChangeEnemyHpAsync(goblin.ID, HpChange.Heal, 1));
        Assert.Equal(3, await CurrentHpAsync(goblin.ID));

        Assert.Equal("Give an amount of at least 1.", await _desk.ChangeEnemyHpAsync(goblin.ID, HpChange.Damage, 0));
    }

    [Fact]
    public async Task Enemies_InAnEndedFight_AreLeftAlone()
    {
        var goblin = await OneGoblinAsync();

        Assert.Null(await _desk.EndAsync(Ford.ID));

        Assert.Equal("That enemy isn't in a fight going on any more.", await _desk.ChangeEnemyHpAsync(goblin.ID, HpChange.Damage, 1));
        Assert.Equal("That enemy isn't in a fight going on any more.", await _desk.RemoveEnemyAsync(goblin.ID));
        Assert.Equal("There's no fight going on here.", await _desk.EndAsync(Ford.ID));
    }

    [Fact]
    public async Task EditEnemy_KeepsItsName()
    {
        var goblin = await OneGoblinAsync();

        Assert.Null(await _desk.EditEnemyAsync(goblin.ID, new Enemy { Name = "Renamed", MaxHP = 6, CurrentHP = 5, Armor = 0, Notes = " Cowardly " }));

        await using var context = _services.CreateContext();
        var edited = await context.Enemies.SingleAsync();
        Assert.Equal(goblin.Name, edited.Name);
        Assert.Equal(6, edited.MaxHP);
        Assert.Equal(5, edited.CurrentHP);
        Assert.Equal(0, edited.Armor);
        Assert.Equal("Cowardly", edited.Notes);
    }

    [Fact]
    public async Task RequestMove_IsRefusedWhileTheLastIsStillWaiting()
    {
        var goblin = await OneGoblinAsync();
        MoveRequest Request() => new() { EncounterID = goblin.EncounterID, EnemyID = goblin.ID, Kind = MoveRequestKind.EnemyAttack };

        var first = await _desk.RequestMoveAsync(Request());
        var doubleClick = await _desk.RequestMoveAsync(Request());
        _time.Advance(MoveRequestRules.GiveUpAfter + TimeSpan.FromSeconds(1));
        var later = await _desk.RequestMoveAsync(Request());

        Assert.Null(first.Error);
        Assert.Equal("Still waiting on the last move. Give the bot a moment.", doubleClick.Error);
        Assert.Null(later.Error);
        Assert.NotEqual(first.ID, later.ID);
    }

    private async Task<int> CurrentHpAsync(int enemyID)
    {
        await using var context = _services.CreateContext();
        return (await context.Enemies.SingleAsync(e => e.ID == enemyID)).CurrentHP;
    }
}

using DungeonWorldBot.Dashboard.Models.Admin;
using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Dashboard.Services.Admin;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Primitives;

namespace DungeonWorldBot.Tests.Dashboard;

/// <summary>
/// The admin desk opens the databases by their paths, so these run against a migrated copy saved to a file.
/// </summary>
public sealed class AdminDeskTests : IAsyncLifetime
{
    private readonly string _folder = Directory.CreateTempSubdirectory("admin-desk-").FullName;
    private readonly AdminSchema _schema;
    private readonly AdminDesk _desk;
    private readonly AdminConsole _console;

    public AdminDeskTests()
    {
        _schema = new AdminSchema(new DashboardDatabases(Path.Combine(_folder, "game.db"), Path.Combine(_folder, "canvases.db")));
        _desk = new AdminDesk(_schema);
        _console = new AdminConsole(_schema);
    }

    public async Task InitializeAsync()
    {
        using var db = new TestDatabase();
        await using (var context = db.CreateContext())
        {
            var service = new EncounterService(context, TimeProvider.System);
            var ambush = (await service.StartAsync("Ambush"))!;
            for (var i = 1; i <= 3; i++)
                await service.AddEnemyAsync(ambush, new Enemy { Name = $"Goblin {i}", MaxHP = 4, CurrentHP = 4 });
        }

        db.CopyTo(Path.Combine(_folder, "game.db"));
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
        return Task.CompletedTask;
    }

    private static FormCollection Form(Dictionary<string, StringValues> values) => new(values);

    [Fact]
    public async Task Tables_AreListedWithTheirRowCounts()
    {
        var tables = await _schema.GetTablesAsync(AdminDatabase.Game);

        Assert.Equal(3, tables.Single(t => t.Name == "Enemies").Rows);
        Assert.Equal(1, tables.Single(t => t.Name == "Encounters").Rows);
        Assert.DoesNotContain(tables, t => t.Name.StartsWith("sqlite_"));
    }

    [Fact]
    public async Task Columns_KnowWhatTheyPointAt()
    {
        var columns = await _schema.GetColumnsAsync(AdminDatabase.Game, "enemies");

        Assert.NotNull(columns);
        Assert.Equal("Encounters", columns.Single(c => c.Name == "EncounterID").References);
        Assert.True(columns.Single(c => c.Name == "ID").PrimaryKey);
        Assert.Null(await _schema.GetColumnsAsync(AdminDatabase.Game, "NoSuchTable"));
    }

    [Fact]
    public async Task Pages_CanBeSearchedAndSorted()
    {
        var page = await _desk.GetPageAsync(AdminDatabase.Game, "Enemies", search: "goblin", sort: "Name", descending: true, page: 1);

        Assert.NotNull(page);
        Assert.Equal(3, page.Matching);
        var name = page.Columns.ToList().FindIndex(c => c.Name == "Name");
        Assert.Equal(["Goblin 3", "Goblin 2", "Goblin 1"], page.Rows.Select(r => r.Values[name]));

        var none = await _desk.GetPageAsync(AdminDatabase.Game, "Enemies", search: "dragon", sort: null, descending: false, page: 1);
        Assert.Equal(0, none!.Matching);
        Assert.Equal(1, none.Pages);
    }

    [Fact]
    public async Task Save_ChangesARow_AndChecksNumbers()
    {
        var first = (await _desk.GetPageAsync(AdminDatabase.Game, "Enemies", null, null, false, 1))!.Rows[0];

        Assert.Null(await _desk.SaveAsync(AdminDatabase.Game, "Enemies", first.RowID, Form(new() { ["v:CurrentHP"] = "1" })));
        Assert.Equal("CurrentHP takes a whole number.",
            await _desk.SaveAsync(AdminDatabase.Game, "Enemies", first.RowID, Form(new() { ["v:CurrentHP"] = "lots" })));

        var columns = (await _schema.GetColumnsAsync(AdminDatabase.Game, "Enemies"))!.ToList();
        var saved = (await _desk.GetRowAsync(AdminDatabase.Game, "Enemies", first.RowID))!;
        Assert.Equal(1L, saved.Values[columns.FindIndex(c => c.Name == "CurrentHP")]);
    }

    [Fact]
    public async Task Save_LeavesNothingWhereABlankCanBe()
    {
        var first = (await _desk.GetPageAsync(AdminDatabase.Game, "Encounters", null, null, false, 1))!.Rows[0];
        var columns = (await _schema.GetColumnsAsync(AdminDatabase.Game, "Encounters"))!.ToList();

        Assert.Null(await _desk.SaveAsync(AdminDatabase.Game, "Encounters", first.RowID,
            Form(new() { ["v:SceneName"] = "", ["v:Name"] = "" })));

        // A scene's name can be nothing; a fight's own name can't, so it's kept as empty text
        var saved = (await _desk.GetRowAsync(AdminDatabase.Game, "Encounters", first.RowID))!;
        Assert.Null(saved.Values[columns.FindIndex(c => c.Name == "SceneName")]);
        Assert.Equal("", saved.Values[columns.FindIndex(c => c.Name == "Name")]);
    }

    [Fact]
    public async Task Save_ExplainsARowPointingAtNothing()
    {
        var error = await _desk.SaveAsync(AdminDatabase.Game, "Enemies", rowID: null,
            Form(new()
            {
                ["v:Name"] = "Stray", ["v:MaxHP"] = "1", ["v:CurrentHP"] = "1", ["v:Armor"] = "0", ["v:EncounterID"] = "999",
            }));

        Assert.StartsWith("The database said no:", error);
    }

    [Fact]
    public async Task Delete_TakesWhatHangsOffTheRowWithIt()
    {
        var fight = (await _desk.GetPageAsync(AdminDatabase.Game, "Encounters", null, null, false, 1))!.Rows.Single();

        Assert.Null(await _desk.DeleteAsync(AdminDatabase.Game, "Encounters", fight.RowID));

        var tables = await _schema.GetTablesAsync(AdminDatabase.Game);
        Assert.Equal(0, tables.Single(t => t.Name == "Enemies").Rows);
    }

    [Fact]
    public async Task Run_ShowsTheFirstRowsRead_OrHowManyChanged()
    {
        var read = await _console.RunAsync(AdminDatabase.Game, "SELECT Name FROM Enemies ORDER BY Name");
        var changed = await _console.RunAsync(AdminDatabase.Game, "UPDATE Enemies SET Armor = 2");
        var broken = await _console.RunAsync(AdminDatabase.Game, "SELEC nonsense");
        var blank = await _console.RunAsync(AdminDatabase.Game, "  ");

        Assert.Equal(["Name"], read.Columns);
        Assert.Equal(["Goblin 1", "Goblin 2", "Goblin 3"], read.Rows.Select(r => r[0]));
        Assert.Null(read.Changed);
        Assert.Equal(3, changed.Changed);
        Assert.NotNull(broken.Error);
        Assert.Equal("Type some SQL to run.", blank.Error);
    }
}

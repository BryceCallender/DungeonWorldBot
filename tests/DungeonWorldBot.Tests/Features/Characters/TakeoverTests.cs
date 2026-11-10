using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Characters;

public sealed class TakeoverTests : IAsyncLifetime, IDisposable
{
    private static readonly Snowflake Bryce = new(1);
    private static readonly Snowflake Elden = new(2);
    private static readonly Snowflake Mira = new(3);

    private readonly TestDatabase _db = new();

    public async Task InitializeAsync()
    {
        await AddAsync(Bryce, "Arthur", ClassType.Paladin);
        await AddAsync(Elden, "Elden", ClassType.Wizard);
        await AddAsync(Mira, "Mira", ClassType.Thief);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _db.Dispose();

    private record RanBy(Snowflake? ID) : ICommandUser;

    private CharacterService ServiceFor(DungeonWorldContext context, Snowflake? ranBy = null) => new(context, new RanBy(ranBy));

    private async Task AddAsync(Snowflake id, string name, ClassType type)
    {
        await using var context = _db.CreateContext();
        await ServiceFor(context).AddCharacterAsync(new Character
        {
            ID = id,
            Name = name,
            Class = new Class { Type = type },
            Health = new Health { CurrentHP = 10, MaxHP = 10 },
            Inventory = new Inventory(),
        });
    }

    [Fact]
    public async Task TakingOver_MakesTheirCommandsActOnThatCharacter()
    {
        await using var context = _db.CreateContext();
        await ServiceFor(context).TakeOverAsync(Elden, Bryce);

        var service = ServiceFor(context, ranBy: Bryce);
        Assert.Equal("Elden", (await service.GetCharacterFromUserAsync(Bryce))!.Name);
        Assert.Equal("Elden", (await service.GetCharacterMovesAsync(Bryce))!.Name);
        Assert.Equal("Elden", await service.GetCharacterNameAsync(Bryce));
    }

    [Fact]
    public async Task TakingOver_LeavesThemAsThemselvesToEveryoneElse()
    {
        await using var context = _db.CreateContext();
        await ServiceFor(context).TakeOverAsync(Elden, Bryce);

        // Someone else picking them out, or the dashboard, still finds their own character
        Assert.Equal("Arthur", (await ServiceFor(context, ranBy: Mira).GetCharacterFromUserAsync(Bryce))!.Name);
        Assert.Equal("Arthur", (await new CharacterService(context).GetCharacterFromUserAsync(Bryce))!.Name);
    }

    [Fact]
    public async Task TakingOverSomeoneNew_LetsGoOfWhoTheyWerePlaying()
    {
        await using var context = _db.CreateContext();
        var service = ServiceFor(context);
        await service.TakeOverAsync(Elden, Bryce);
        await service.TakeOverAsync(Mira, Bryce);

        var names = await service.GetCharacterNamesAsync();
        Assert.Null(names.Single(c => c.ID == Elden).ControlledByID);
        Assert.Equal(Bryce, names.Single(c => c.ID == Mira).ControlledByID);
    }

    [Fact]
    public async Task Releasing_GivesThemTheirOwnCharacterBack()
    {
        await using var context = _db.CreateContext();
        await ServiceFor(context).TakeOverAsync(Elden, Bryce);
        await ServiceFor(context).ReleaseAsync(Elden);

        Assert.Equal("Arthur", (await ServiceFor(context, ranBy: Bryce).GetCharacterFromUserAsync(Bryce))!.Name);
    }
}

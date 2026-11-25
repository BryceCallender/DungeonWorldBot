using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Party;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Dashboard.Services.Party;
using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Resources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Dashboard;

public sealed class SheetRollsTests : IDisposable
{
    private const ulong ArthurID = 77;
    private const ulong BedivereID = 78;

    private static readonly Viewer Arthur = new(true, ArthurID, "Arthur", null, false, false);
    private static readonly Viewer Bedivere = new(true, BedivereID, "Bedivere", null, false, false);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 9, 20, 0, 0, TimeSpan.Zero));
    private readonly DashboardTestServices _services;
    private readonly DungeonWorldContext _context;
    private readonly SheetRolls _rolls;

    public SheetRollsTests()
    {
        _services = new DashboardTestServices(_time);
        _context = _services.CreateContext();
        _rolls = new SheetRolls(
            new CharacterService(_context),
            new RollableMoves(new CharacterMoves(new ResourceService(_context))),
            new EncounterDesk(_services.Writes, _time),
            _context,
            _time);
    }

    public void Dispose()
    {
        _context.Dispose();
        _services.Dispose();
    }

    private async Task AddPartyAsync()
    {
        await _services.AddCharacterAsync(ArthurID, "Arthur");
        await _services.AddCharacterAsync(BedivereID, "Bedivere");
    }

    private async Task<MoveRequest> OnlyRequestAsync()
    {
        await using var context = _services.CreateContext();
        return await context.MoveRequests.SingleAsync();
    }

    [Fact]
    public async Task Ask_ForTheirOwnCharacter_AsksTheBotWithNoFight()
    {
        await AddPartyAsync();

        var asked = await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("defy danger", false, StatType.Dex, Bonus: 1));

        Assert.Null(asked.Error);
        var request = await OnlyRequestAsync();
        Assert.Equal(asked.ID, request.ID);
        Assert.Null(request.EncounterID);
        Assert.Equal((MoveRequestKind.BasicMove, "Defy Danger", StatType.Dex, 1), (request.Kind, request.Move, request.Stat, request.Bonus));
        Assert.Equal(ArthurID, request.CharacterID?.Value);
        Assert.Equal(ArthurID, request.RequestedBy?.Value);
    }

    [Fact]
    public async Task Ask_AidOrInterfere_AimsAtTheAllyPicked()
    {
        await AddPartyAsync();

        var asked = await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll(BasicMoves.AidOrInterfere.Name, false, Ally: BedivereID));

        Assert.Null(asked.Error);
        Assert.Equal(BedivereID, (await OnlyRequestAsync()).TargetCharacterID?.Value);
    }

    [Fact]
    public async Task Ask_AsSomeoneElsesCharacter_OrWithoutWhatTheMoveNeeds_IsRefused()
    {
        await AddPartyAsync();

        Assert.Equal("You can only roll as your own character.",
            (await _rolls.AskAsync(ArthurID, Bedivere, new SheetRoll("Spout Lore", false))).Error);
        Assert.Equal("Pick a stat to roll Defy Danger with.",
            (await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Defy Danger", false))).Error);
        Assert.Equal("Pick whose character you're helping or hindering.",
            (await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll(BasicMoves.AidOrInterfere.Name, false, Ally: ArthurID))).Error);
        Assert.Equal("Arthur can't make Fireball.",
            (await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Fireball", true))).Error);
        Assert.Equal("Keep bonuses between -10 and +10.",
            (await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Spout Lore", false, Bonus: 11))).Error);

        await using var context = _services.CreateContext();
        Assert.Empty(context.MoveRequests);
    }

    [Fact]
    public async Task Ask_WhileTheLastIsStillWaiting_IsRefused()
    {
        await AddPartyAsync();
        await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Spout Lore", false));

        var again = await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Spout Lore", false));

        Assert.Equal("Still waiting on the last move. Give the bot a moment.", again.Error);
    }

    [Fact]
    public async Task Status_WaitsForTheBot_ThenSaysHowItWent()
    {
        await AddPartyAsync();
        var asked = await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Spout Lore", false));

        Assert.Equal(SheetRollStatus.Waiting, await _rolls.StatusAsync(ArthurID, asked.ID));
        // Only their own
        Assert.Null(await _rolls.StatusAsync(BedivereID, asked.ID));

        await using (var context = _services.CreateContext())
        {
            var request = await context.MoveRequests.SingleAsync();
            request.ResolvedAt = _time.GetUtcNow();
            request.Summary = "Weak hit";
            await context.SaveChangesAsync();
        }

        Assert.Equal(new SheetRollStatus(true, "Weak hit"), await _rolls.StatusAsync(ArthurID, asked.ID));
    }

    [Fact]
    public async Task Status_OfARollTheBotNeverGotTo_GivesUp()
    {
        await AddPartyAsync();
        var asked = await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Spout Lore", false));

        _time.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(new SheetRollStatus(true, Error: "The bot didn't roll it. Is it running?"), await _rolls.StatusAsync(ArthurID, asked.ID));
    }

    [Fact]
    public async Task Ask_WhileTheyveTakenOverAnother_RollsForThatOne_NotTheirOwn()
    {
        await AddPartyAsync();
        await using (var context = _services.CreateContext())
            await new CharacterService(context).TakeOverAsync(new Snowflake(BedivereID), new Snowflake(ArthurID));

        Assert.True(await _rolls.CanRollAsync(Arthur, BedivereID));
        Assert.False(await _rolls.CanRollAsync(Arthur, ArthurID));
        Assert.Null((await _rolls.AskAsync(BedivereID, Arthur, new SheetRoll("Spout Lore", false))).Error);
        Assert.Equal("You can only roll as your own character.",
            (await _rolls.AskAsync(ArthurID, Arthur, new SheetRoll("Spout Lore", false))).Error);
    }

    [Fact]
    public void Played_IsTheOneTakenOver_WithTheirOwnBesideIt()
    {
        var arthur = new CharacterName(new(ArthurID), "Arthur", ClassType.Fighter);
        var bedivere = new CharacterName(new(BedivereID), "Bedivere", ClassType.Fighter, ControlledByID: new(ArthurID));

        var played = PlayedCharacters.Of([arthur, bedivere], ArthurID);

        Assert.True(played.Plays(BedivereID));
        Assert.Equal([arthur], played.Others);
        // Bedivere's own player still plays them too, as in Discord
        Assert.True(PlayedCharacters.Of([arthur, bedivere], BedivereID).Plays(BedivereID));
    }
}

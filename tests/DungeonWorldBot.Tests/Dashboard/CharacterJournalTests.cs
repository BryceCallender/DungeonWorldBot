using DungeonWorldBot.Dashboard.Services.Party;
using DungeonWorldBot.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Dashboard;

public sealed class CharacterJournalTests : IDisposable
{
    private const ulong AriaID = 1;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 20, 0, 0, TimeSpan.Zero));
    private readonly DashboardTestServices _services;
    private readonly CharacterJournal _journal;

    public CharacterJournalTests()
    {
        _services = new DashboardTestServices(_time);
        _journal = new CharacterJournal(_services.Writes, _time);
    }

    public void Dispose() => _services.Dispose();

    private async Task<Character> ReadAriaAsync()
    {
        await using var context = _services.CreateContext();
        return await context.Characters.Include(c => c.Goals).Include(c => c.Notes).SingleAsync(c => c.ID == new Snowflake(AriaID));
    }

    [Fact]
    public async Task Backstory_IsTrimmed_AndBlankClearsIt()
    {
        await _services.AddCharacterAsync(AriaID, "Aria");

        Assert.True(await _journal.SetBackstoryAsync(AriaID, "  Raised by wolves.  "));
        Assert.Equal("Raised by wolves.", (await ReadAriaAsync()).Backstory);

        Assert.True(await _journal.SetBackstoryAsync(AriaID, "   "));
        Assert.Null((await ReadAriaAsync()).Backstory);
    }

    [Fact]
    public async Task Goals_CanBeAddedEditedCompletedAndRemoved()
    {
        await _services.AddCharacterAsync(AriaID, "Aria");

        Assert.True(await _journal.AddGoalAsync(AriaID, " Find the hireling "));
        var goal = Assert.Single((await ReadAriaAsync()).Goals);
        Assert.Equal("Find the hireling", goal.Text);

        Assert.True(await _journal.EditGoalAsync(AriaID, goal.ID, "Find the hireling's killer"));
        Assert.True(await _journal.SetGoalCompletedAsync(AriaID, goal.ID, true));
        goal = Assert.Single((await ReadAriaAsync()).Goals);
        Assert.Equal("Find the hireling's killer", goal.Text);
        Assert.True(goal.Completed);

        Assert.True(await _journal.RemoveGoalAsync(AriaID, goal.ID));
        Assert.Empty((await ReadAriaAsync()).Goals);
    }

    [Fact]
    public async Task Notes_AreStampedWithTheTime()
    {
        await _services.AddCharacterAsync(AriaID, "Aria");

        Assert.True(await _journal.AddNoteAsync(AriaID, "Owes Bram 5 coins"));

        var note = Assert.Single((await ReadAriaAsync()).Notes);
        Assert.Equal("Owes Bram 5 coins", note.Text);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, note.CreatedAt);
    }

    [Fact]
    public async Task Changes_ToSomethingThatIsntThere_AreRefused()
    {
        await _services.AddCharacterAsync(AriaID, "Aria");

        Assert.False(await _journal.AddGoalAsync(AriaID + 1, "Nobody's goal"));
        Assert.False(await _journal.EditGoalAsync(AriaID, goalID: 99, "Missing"));
        Assert.False(await _journal.RemoveNoteAsync(AriaID, noteID: 99));
    }

    [Fact]
    public async Task OneCharactersNotes_CantBeChangedThroughAnother()
    {
        await _services.AddCharacterAsync(AriaID, "Aria");
        await _services.AddCharacterAsync(2, "Bram");
        await _journal.AddNoteAsync(AriaID, "Aria's secret");
        var note = Assert.Single((await ReadAriaAsync()).Notes);

        Assert.False(await _journal.EditNoteAsync(2, note.ID, "Bram was here"));
        Assert.False(await _journal.RemoveNoteAsync(2, note.ID));
        Assert.Equal("Aria's secret", Assert.Single((await ReadAriaAsync()).Notes).Text);
    }
}

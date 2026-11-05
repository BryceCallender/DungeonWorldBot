using d20Tek.DiceNotation.DieRoller;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Characters;

public class CreationSessionsTests
{
    private static readonly Snowflake Player = new(5);

    private readonly FakeTimeProvider _clock = new();

    private CreationSessions Sessions() =>
        new(new DiceRoller(new ConstantDieRoller(3)), Options.Create(new BotSettings()), _clock);

    [Fact]
    public void ADraft_IsPickedUpAgain_UntilItsBeenLeftTooLong()
    {
        var sessions = Sessions();
        var (started, resumed) = sessions.Resume(Player);
        Assert.False(resumed);

        // Touched again within the six hours, so it keeps going
        _clock.Advance(TimeSpan.FromHours(5));
        Assert.Same(started, sessions.Resume(Player).Session);

        _clock.Advance(TimeSpan.FromHours(5));
        Assert.True(sessions.Resume(Player).Resumed);

        // Left longer than that, it's gone and a new one starts
        _clock.Advance(TimeSpan.FromHours(7));
        Assert.Null(sessions.Find(Player));
        Assert.False(sessions.Resume(Player).Resumed);
    }
}

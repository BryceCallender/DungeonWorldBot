using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Tests;

public class StatTests
{
    [Theory]
    [InlineData(3, -3)]
    [InlineData(5, -2)]
    [InlineData(8, -1)]
    [InlineData(9, 0)]
    [InlineData(12, 0)]
    [InlineData(13, 1)]
    [InlineData(15, 1)]
    [InlineData(16, 2)]
    [InlineData(18, 3)]
    public void Modifier_MatchesDungeonWorldTable(int value, int expected)
    {
        Assert.Equal(expected, new Stat { Value = value }.Modifier);
    }
}

using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Responder;

namespace DungeonWorldBot.Tests;

public class DiceMessageTests
{
    [Theory]
    [InlineData(".2d6", "2d6", null)]
    [InlineData(".d20", "d20", null)]
    [InlineData(". 2d6 + 1", "2d6+1", null)]
    [InlineData(".2d6+1 sneak attack", "2d6+1", "sneak attack")]
    [InlineData(".1d8 damage to the goblin", "1d8", "damage to the goblin")]
    [InlineData(".4d6k3 strength", "4d6k3", "strength")]
    [InlineData(".3d6! fireball", "3d6!", "fireball")]
    [InlineData(".(2d6+1)x2 crit", "(2d6+1)x2", "crit")]
    [InlineData(".4f", "4f", null)]
    [InlineData(".d% loot table", "d%", "loot table")]
    public void DiceRolls_SplitExpressionAndLabel(string content, string expression, string? label)
    {
        var parsed = DiceMessage.TryParse(content);

        Assert.NotNull(parsed);
        Assert.Equal(expression, parsed.Expression);
        Assert.Equal(label, parsed.Label);
        Assert.Null(parsed.Stat);
    }

    [Theory]
    [InlineData(".dex", StatType.Dex, 0, null)]
    [InlineData(".STR", StatType.Str, 0, null)]
    [InlineData(".wis+1 search the room", StatType.Wis, 1, "search the room")]
    [InlineData(".cha-2", StatType.Cha, -2, null)]
    public void StatRolls_AreRecognised(string content, StatType stat, int bonus, string? label)
    {
        var parsed = DiceMessage.TryParse(content);

        Assert.NotNull(parsed);
        Assert.Equal(stat, parsed.Stat);
        Assert.Equal(bonus, parsed.Bonus);
        Assert.Equal(label, parsed.Label);
        Assert.Null(parsed.Expression);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("...")]
    [InlineData(". . .")]
    [InlineData(".hello")]
    [InlineData(".com is a domain")]
    [InlineData(".5")]
    [InlineData("2d6")]
    [InlineData("hello .2d6")]
    [InlineData(".1000d6")]
    [InlineData(".60d6+60d6")]
    [InlineData(".d0")]
    [InlineData(".d100000")]
    [InlineData(".99999999999d6")]
    public void NonRolls_AreIgnored(string content)
    {
        Assert.Null(DiceMessage.TryParse(content));
    }
}

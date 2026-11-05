using DungeonWorldBot.Infrastructure;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;

namespace DungeonWorldBot.Tests.Infrastructure;

public class EmbedLimitsTests
{
    [Fact]
    public void LongParts_AreCutShort()
    {
        var fields = Enumerable.Range(0, 30).Select(i => (IEmbedField)new EmbedField($"Field {i}", new string('v', 2000))).ToList();

        var fitted = EmbedLimits.Fit(new Embed(Title: new string('t', 300), Description: new string('d', 5000), Fields: fields));

        Assert.Equal(256, fitted.Title.Value.Length);
        Assert.EndsWith("…", fitted.Title.Value);
        Assert.Equal(4096, fitted.Description.Value.Length);
        Assert.Equal(25, fitted.Fields.Value.Count);
        Assert.All(fitted.Fields.Value, f => Assert.Equal(1024, f.Value.Length));
    }

    [Fact]
    public void ShortParts_AreLeftAlone()
    {
        var embed = new Embed(Title: "Title", Description: "Description", Fields: new List<IEmbedField> { new EmbedField("Name", "Value", true) });

        var fitted = EmbedLimits.Fit(embed);

        Assert.Equal("Title", fitted.Title.Value);
        Assert.Equal("Description", fitted.Description.Value);
        var field = Assert.Single(fitted.Fields.Value);
        Assert.Equal(("Name", "Value", true), (field.Name, field.Value, field.IsInline.Value));
    }

    [Fact]
    public void EmptyDescriptions_AreLeftOff() =>
        Assert.False(EmbedLimits.Fit(new Embed(Title: "Title", Description: "")).Description.HasValue);

    [Theory]
    [InlineData(2, "+2")]
    [InlineData(0, "+0")]
    [InlineData(-1, "-1")]
    public void Modifiers_ShowTheirSign(int value, string shown) => Assert.Equal(shown, Numbers.Signed(value));
}

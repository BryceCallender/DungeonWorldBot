using DungeonWorldBot.Dashboard.Services.Bridge;
using DungeonWorldBot.Features.Bridge;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.Guides;

namespace DungeonWorldBot.Tests.Dashboard;

public class PlayPanelSectionsTests
{
    private static CatalogCommand Command(string path, ClassMechanic? mechanic = null) =>
        new(path, "", path.Split(' ')[0], null, false, [], mechanic);

    private static readonly Dictionary<string, CatalogCommand> Catalog = new[]
    {
        Command("shapeshift into", ClassMechanic.Shapeshifting),
        Command("shapeshift forms", ClassMechanic.Shapeshifting),
        Command("eclipse aspect", ClassMechanic.CircleOfTheEclipse),
        Command("subclass info"),
        Command("class sheet"),
    }.ToDictionary(c => c.Path);

    // Shaped like a druid's guide from CharacterCommands
    private static readonly IReadOnlyList<GuideStep> Guide =
    [
        new("Basic moves", "", [new("/move hack-and-slash"), new("/move defy-danger")]),
        new("Class", "", [new("/move class", "Hunt and Track"), new("/class sheet")]),
        new("Shapeshifting", "", [new("/shapeshift into"), new("/shapeshift forms")]),
        new("Resources", "", [new("/resource show"), new("/resource gain", "Preparation"), new("/resource spend", "Preparation")]),
        new("Circle of the Eclipse", "", [new("/eclipse aspect"), new("/subclass info", "Circle of the Eclipse")]),
        new("Downtime", "", [new("/downtime make-camp")]),
    ];

    [Fact]
    public void ClassAndSubclassMoves_Mechanics_Resources_ThenBasicMoves_ComeFirst()
    {
        var sections = PlayPanelSections.Of(Guide, Catalog, []);

        Assert.Equal(["Class moves", "Shapeshifting", "Circle of the Eclipse", "Resources", "Basic moves"], sections.First.Select(s => s.Title));
        Assert.Equal(["/move class"], sections.First[0].Commands.Select(c => c.Command));
        Assert.Equal(["Preparation", "Preparation"], sections.First[3].Commands.Select(c => c.Pick));
    }

    [Fact]
    public void EverythingElse_KeepsItsStep_ForTheFold()
    {
        var sections = PlayPanelSections.Of(Guide, Catalog, []);

        Assert.Equal(["Class", "Resources", "Circle of the Eclipse", "Downtime"], sections.Rest.Select(s => s.Title));
        Assert.Equal(["/class sheet", "/resource show", "/subclass info", "/downtime make-camp"],
            sections.Rest.SelectMany(s => s.Commands).Select(c => c.Command));
    }

    [Fact]
    public void AMechanicTheGuideDoesntList_JoinsTheClassSections()
    {
        var sections = PlayPanelSections.Of(Guide, Catalog, [Command("jar raise", ClassMechanic.HexedJars), Command("jar open", ClassMechanic.HexedJars)]);

        var jars = Assert.Single(sections.First, s => s.Title == "Hexed jars");
        Assert.Equal(["/jar raise", "/jar open"], jars.Commands.Select(c => c.Command));
        Assert.True(sections.First.ToList().IndexOf(jars) < sections.First.ToList().FindIndex(s => s.Title == "Resources"));
    }
}

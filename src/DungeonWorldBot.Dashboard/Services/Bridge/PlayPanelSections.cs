using DungeonWorldBot.Dashboard.Models.Bridge;
using DungeonWorldBot.Features.Bridge;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Classes.Guides;

namespace DungeonWorldBot.Dashboard.Services.Bridge;

/// <summary>
/// Sorts a character's guide (see <see cref="CharacterCommands"/>) for the Play panel: what's played mid-fight first,
/// the rest folded away.
/// </summary>
public static class PlayPanelSections
{
    private const string ClassMove = "move class";

    /// <param name="catalog">The bot's commands by path, for which belong to a class mechanic like Shapeshifting.</param>
    /// <param name="unlisted">Commands for the character's class mechanics that the guide doesn't list, like the
    /// Necromancer's jars, shown with the rest of their class's.</param>
    public static PanelSections Of(IReadOnlyList<GuideStep> guide, IReadOnlyDictionary<string, CatalogCommand> catalog, IEnumerable<CatalogCommand> unlisted)
    {
        var commands = guide.SelectMany(s => s.Commands).ToList();

        // A mechanic's step keeps its own title, like "Shapeshifting" or the subclass's name
        var mechanics = guide
            .Select(s => s with { Commands = s.Commands.Where(c => PathOf(c) != ClassMove && catalog.GetValueOrDefault(PathOf(c))?.Mechanic is not null).ToList() })
            .ToList();

        IReadOnlyList<GuideStep> first =
        [
            Step("Class moves", commands.Where(c => PathOf(c) == ClassMove)),
            .. mechanics,
            .. unlisted.GroupBy(c => c.Mechanic).Select(g => Step(TitleOf(g.Key), g.Select(c => new GuideCommand($"/{c.Path}")))),
            Step("Resources", commands.Where(c => GroupOf(c) == "resource" && c.Pick is not null)),
            Step("Basic moves", commands.Where(c => GroupOf(c) == "move" && PathOf(c) != ClassMove)),
        ];
        first = first.Where(s => s.Commands.Count > 0).ToList();

        var shown = first.SelectMany(s => s.Commands).ToHashSet();
        var rest = guide
            .Select(s => s with { Commands = s.Commands.Where(c => !shown.Contains(c)).ToList() })
            .Where(s => s.Commands.Count > 0)
            .ToList();
        return new PanelSections(first, rest);
    }

    private static GuideStep Step(string title, IEnumerable<GuideCommand> commands) => new(title, string.Empty, commands.ToList());

    // "HexedJars" reads as "Hexed jars"
    private static string TitleOf(ClassMechanic? mechanic)
    {
        var name = mechanic?.ToString() ?? "Class";
        return name[0] + string.Concat(name[1..].Select(ch => char.IsUpper(ch) ? $" {char.ToLowerInvariant(ch)}" : ch.ToString()));
    }

    private static string PathOf(GuideCommand command) => command.Command.TrimStart('/');

    private static string GroupOf(GuideCommand command) => PathOf(command).Split(' ')[0];
}

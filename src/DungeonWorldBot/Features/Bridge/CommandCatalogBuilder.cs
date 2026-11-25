using System.ComponentModel;
using System.Reflection;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Conditions;
using Remora.Commands.Services;
using Remora.Commands.Signatures;
using Remora.Commands.Trees;
using Remora.Commands.Trees.Nodes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Bridge;

/// <summary>
/// The bot's slash commands as the dashboard offers them (see <see cref="CommandCatalog"/>), read off the same command
/// tree Discord is told about, so a new command shows up on the dashboard without anything else to do.
/// </summary>
[Scoped]
public class CommandCatalogBuilder(CommandService commands, TimeProvider time)
{
    /// <summary>
    /// Commands the dashboard can't run like the others: creating a character is a back-and-forth of Discord buttons.
    /// </summary>
    public static readonly IReadOnlySet<string> NotFromTheDashboard = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "character create",
    };

    // What Remora calls a group or option without a [Description]
    private const string NoDescription = "No description set.";

    public CommandCatalog Build() =>
        new(time.GetUtcNow(), Commands().Select(c => Describe(c.Path, c.Node)).ToList());

    /// <summary>
    /// A command the dashboard can run, by its path, e.g. "shapeshift into".
    /// </summary>
    public CommandNode? Find(string path) =>
        Commands().FirstOrDefault(c => c.Path.Equals(path.Trim(), StringComparison.OrdinalIgnoreCase))?.Node;

    private record FoundCommand(string Path, CommandNode Node);

    private IEnumerable<FoundCommand> Commands()
    {
        if (!commands.TreeAccessor.TryGetNamedTree(null, out var tree) || tree is null)
            return [];

        return Walk(tree.Root, [])
            .Where(c => IsSlashCommand(c.Node) && !NotFromTheDashboard.Contains(c.Path))
            .OrderBy(c => c.Path, StringComparer.Ordinal);
    }

    private static IEnumerable<FoundCommand> Walk(IParentNode parent, IReadOnlyList<string> path)
    {
        foreach (var child in parent.Children)
        {
            var here = path.Append(child.Key).ToList();
            switch (child)
            {
                case CommandNode command:
                    yield return new FoundCommand(string.Join(' ', here), command);
                    break;
                case IParentNode group:
                    foreach (var found in Walk(group, here))
                        yield return found;
                    break;
            }
        }
    }

    // Message and user commands, like Change outcome, are run from a message's menu, not by name
    private static bool IsSlashCommand(CommandNode command) =>
        command.Attributes.OfType<CommandTypeAttribute>().FirstOrDefault() is not { } type
        || type.Type == ApplicationCommandType.ChatInput;

    private static CatalogCommand Describe(string path, CommandNode command)
    {
        var top = TopGroup(command);
        return new CatalogCommand(
            path,
            command.Shape.Description,
            path.Split(' ')[0],
            top?.Description is { } described && described != NoDescription ? described : null,
            IsGameMasterOnly(command),
            command.Shape.Parameters.Select(Describe).ToList(),
            MechanicOf(command),
            TitleOf(command));
    }

    /// <summary>
    /// The command's [DisplayName], what the dashboard calls it in place of its slash command.
    /// </summary>
    public static string? TitleOf(CommandNode command) =>
        command.Attributes.OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName;

    // Marked on the command itself, or on a group it's in
    private static ClassMechanic? MechanicOf(CommandNode command)
    {
        IEnumerable<Attribute> attributes = command.Attributes;
        for (var parent = command.Parent; parent is GroupNode group; parent = group.Parent)
            attributes = attributes.Concat(group.Attributes);
        return attributes.OfType<ForClassMechanicAttribute>().FirstOrDefault()?.Mechanic;
    }

    private static GroupNode? TopGroup(IChildNode node)
    {
        GroupNode? top = null;
        for (var parent = node.Parent; parent is GroupNode group; parent = group.Parent)
            top = group;
        return top;
    }

    private static bool IsGameMasterOnly(CommandNode command)
    {
        IEnumerable<ConditionAttribute> conditions = command.Conditions;
        for (var parent = command.Parent; parent is GroupNode group; parent = group.Parent)
            conditions = conditions.Concat(group.Conditions);
        return conditions.OfType<RequireGameMasterAttribute>().Any();
    }

    private static CatalogOption Describe(IParameterShape parameter)
    {
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        var kind = KindOf(type);
        return new CatalogOption(
            parameter.HintName,
            parameter.Description == NoDescription ? string.Empty : parameter.Description,
            kind,
            !parameter.IsOmissible(new TreeSearchOptions(StringComparison.OrdinalIgnoreCase)),
            DefaultOf(parameter),
            kind == CatalogOptionType.Choice ? ChoicesOf(type) : [],
            parameter.Attributes.OfType<AutocompleteProviderAttribute>().Any(),
            NumberOf(parameter.Attributes.OfType<MinValueAttribute>().FirstOrDefault()?.Value.Value),
            NumberOf(parameter.Attributes.OfType<MaxValueAttribute>().FirstOrDefault()?.Value.Value));
    }

    private static CatalogOptionType KindOf(Type type) => type switch
    {
        _ when type.IsEnum => CatalogOptionType.Choice,
        _ when type == typeof(bool) => CatalogOptionType.Boolean,
        _ when type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(uint) || type == typeof(ulong) => CatalogOptionType.Integer,
        _ when type == typeof(double) || type == typeof(float) || type == typeof(decimal) => CatalogOptionType.Number,
        _ when type.IsAssignableTo(typeof(IUser)) || type.IsAssignableTo(typeof(IGuildMember)) => CatalogOptionType.User,
        _ => CatalogOptionType.Text,
    };

    // Each value by its name, as the command takes it, and shown by its description when it has one, as in Discord
    private static IReadOnlyList<CatalogChoice> ChoicesOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => new CatalogChoice(f.GetCustomAttribute<DescriptionAttribute>()?.Description ?? f.Name, f.Name))
            .ToList();

    private static string? DefaultOf(IParameterShape parameter) => parameter.DefaultValue switch
    {
        null or DBNull or Missing => null,
        bool b => b ? "true" : "false",
        var value => value.ToString(),
    };

    private static double? NumberOf(object? value) => value is null ? null : Convert.ToDouble(value);
}

using DungeonWorldBot.Features.Characters;
using Remora.Commands.Groups;
using Remora.Commands.Trees;
using Remora.Commands.Trees.Nodes;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity;

namespace DungeonWorldBot.Tests.Infrastructure;

public class CommandTreeTests
{
    // Registered the same way ServiceExtensions.AddCommands does: top-level groups only, nested ones come with them
    private static CommandTree BuildTree()
    {
        var builder = new CommandTreeBuilder();
        var types = typeof(CharacterCommand).Assembly
            .GetExportedTypes()
            .Where(t => t is { IsClass: true, IsNested: false, IsAbstract: false } && t.IsAssignableTo(typeof(CommandGroup)) && !t.IsAssignableTo(typeof(InteractionGroup)));

        foreach (var type in types)
            builder.RegisterModule(type);

        return builder.Build();
    }

    private static IEnumerable<string> Paths(IParentNode node, string prefix = "") =>
        node.Children.SelectMany(child => child switch
        {
            IParentNode group => Paths(group, $"{prefix}{child.Key} "),
            _ => [$"/{prefix}{child.Key}"]
        });

    [Fact]
    public void Tree_ConvertsToSlashCommands_WithDmHidden()
    {
        var commands = BuildTree().CreateApplicationCommands();

        var dm = Assert.Single(commands, c => c.Name == "dm");
        Assert.NotNull(dm.DefaultMemberPermissions);
    }

    [Theory]
    [InlineData("/subclass join")]
    [InlineData("/subclass eclipse aspect")]
    [InlineData("/subclass luna vow")]
    [InlineData("/subclass guardian read-the-room")]
    [InlineData("/character damage-dice")]
    [InlineData("/character level up")]
    [InlineData("/character level allocate")]
    [InlineData("/character level points")]
    [InlineData("/dm stat set")]
    [InlineData("/dm stat grant-points")]
    [InlineData("/dm level set")]
    [InlineData("/dm level set-points")]
    [InlineData("/dm diag temp")]
    [InlineData("/npc roll")]
    [InlineData("/dm npc add")]
    [InlineData("/dm npc stats")]
    public void Tree_HasNestedCommand(string path)
    {
        Assert.Contains(path, Paths(BuildTree().Root));
    }

    [Theory]
    [InlineData("eclipse")]
    [InlineData("luna")]
    [InlineData("guardian")]
    [InlineData("diag")]
    public void Tree_DoesNotHaveOldTopLevelGroup(string name)
    {
        Assert.DoesNotContain(BuildTree().Root.Children, c => c.Key == name);
    }
}

using System.Text.Json;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Bridge;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Shapeshifting;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Gateway.Extensions;
using Remora.Rest.Core;

namespace DungeonWorldBot.Tests.Features.Bridge;

/// <summary>
/// Real commands run through the bridge, with the bot's own services and Remora's, and Discord faked.
/// </summary>
public sealed class CommandBridgeTests : IDisposable
{
    private const ulong GameChat = 555;
    private static readonly Snowflake DruidID = new(88);

    private readonly TestDatabase _db = new();
    private readonly FakeDiscord _discord = new();
    private readonly ServiceProvider _provider;

    public CommandBridgeTests()
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddDiscordGateway(_ => "token")
            .AddScoped(_ => _db.CreateContext())
            .AddSqlite<CanvasContext>("Data Source=:memory:")
            .AddCoreServices()
            .AddAttributedServices(typeof(Program).Assembly)
            .AddRemoraServices();
        services.AddBotSettings();
        services.Configure<BotSettings>(s => s.GameChatChannelID = GameChat);
        services.AddSingleton(_discord.Channels);
        services.AddSingleton(_discord.Users);
        services.AddSingleton(_discord.Guilds);
        services.AddReplyCapture();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    private async Task AddDruidAsync()
    {
        await using var context = _db.CreateContext();
        var character = new Character
        {
            ID = DruidID,
            Name = "Fern",
            Class = new Class { Type = ClassType.Druid },
            Health = new Health { CurrentHP = 16, MaxHP = 16 },
            Inventory = new Inventory(),
            Stats = [new Stat { StatType = StatType.Wis, Value = 16 }],
            Abilities = [new Ability { Name = ShapeshiftRules.Shapeshifter }],
        };
        await new CharacterService(context).AddCharacterAsync(character);
        await new TransformationService(context, new ResourceService(context)).LearnAsync(character, "Wolf", "Fast and fierce", null);
    }

    private async Task<BridgeResult> SendAsync(string command, Dictionary<string, string> options, CommandRequestKind kind = CommandRequestKind.Run, string? focused = null)
    {
        // A scope of its own, as the worker gives each request
        await using var scope = _provider.CreateAsyncScope();
        var request = new CommandRequest
        {
            UserID = DruidID,
            Kind = kind,
            Command = command,
            Options = JsonSerializer.Serialize(options, BridgeJson.Default.DictionaryStringString),
            Focused = focused,
        };
        var bridge = scope.ServiceProvider.GetRequiredService<CommandBridge>();
        return kind == CommandRequestKind.Suggest
            ? await bridge.SuggestAsync(request, CancellationToken.None)
            : await bridge.RunAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Run_Shapeshift_ChangesTheirShape_AndPostsInGameChat_AsTheyDTyped()
    {
        await AddDruidAsync();

        var result = await SendAsync("shapeshift into", new() { ["form"] = "wolf", ["bonus"] = "1" });

        Assert.Null(result.Error);
        var embed = Assert.Single(Assert.Single(result.Reply!.Messages).Embeds);
        Assert.Equal("🐾 Fern becomes a Wolf", embed.Title);
        Assert.Contains(embed.Fields, f => f.Name == ShapeshiftRules.HoldName);

        var (channel, _) = Assert.Single(_discord.Posted);
        Assert.Equal(GameChat, channel.Value);

        await using var context = _db.CreateContext();
        var fern = await new CharacterService(context).GetCharacterMovesAsync(DruidID);
        Assert.Equal("Wolf", ShapeshiftRules.FormName(fern!));
        // Counted in their roll stats, as a roll in game chat
        Assert.Equal(DruidID, (await context.Rolls.SingleAsync()).UserID);
    }

    [Fact]
    public async Task Run_WithAFormTheyDontKnow_ShowsTheCommandsOwnAnswer()
    {
        await AddDruidAsync();

        var result = await SendAsync("shapeshift into", new() { ["form"] = "Dragon" });

        // The command answered, as it does in Discord, so it ran fine; what it said is the answer
        Assert.Null(result.Error);
        var embed = Assert.Single(Assert.Single(result.Reply!.Messages).Embeds);
        Assert.Contains("Dragon", embed.Description);
    }

    [Fact]
    public async Task Run_ADmCommand_AsAPlayer_IsRefusedWithoutPostingAnything()
    {
        await AddDruidAsync();

        var result = await SendAsync("dm level set", new() { ["level"] = "5" });

        Assert.StartsWith("Only someone with the", result.Error);
        Assert.Empty(_discord.Posted);
    }

    [Fact]
    public async Task Run_WithoutWhatItNeeds_SaysSo()
    {
        await AddDruidAsync();

        var result = await SendAsync("shapeshift into", []);

        Assert.Equal("Fill in everything it needs first.", result.Error);
    }

    [Fact]
    public async Task Run_CharacterCreate_IsLeftToDiscord()
    {
        var result = await SendAsync("character create", []);

        Assert.Equal("The dashboard can't run /character create.", result.Error);
    }

    [Fact]
    public async Task Suggest_TheFormsTheyKnow()
    {
        await AddDruidAsync();

        var result = await SendAsync("shapeshift into", new() { ["form"] = "w" }, CommandRequestKind.Suggest, "form");

        Assert.Null(result.Error);
        Assert.Equal("Wolf", Assert.Single(result.Reply!.Suggestions).Value);
    }

    [Fact]
    public void Catalog_OffersEveryCommand_WithWhatEachTakes()
    {
        using var scope = _provider.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<CommandCatalogBuilder>().Build();

        var into = Assert.Single(catalog.Commands, c => c.Path == "shapeshift into");
        Assert.Equal("shapeshift", into.Group);
        Assert.False(into.GameMasterOnly);
        var form = Assert.Single(into.Options, o => o.Name == "form");
        Assert.True(form is { Required: true, Suggests: true, Type: CatalogOptionType.Text });
        var bonus = Assert.Single(into.Options, o => o.Name == "bonus");
        Assert.True(bonus is { Required: false, Type: CatalogOptionType.Integer, Default: "0" });

        Assert.True(Assert.Single(catalog.Commands, c => c.Path == "dm level set").GameMasterOnly);
        Assert.DoesNotContain(catalog.Commands, c => c.Path == "character create");
        Assert.True(catalog.Commands.Count > 150);
    }

    // Each group that's only for some characters, and what they need: a new one belongs here, marked on its command
    // class with [ForClassMechanic], or the dashboard offers it to everyone
    [Theory]
    [InlineData("shapeshift", ClassMechanic.Shapeshifting)]
    [InlineData("spell", ClassMechanic.Spellcasting)]
    [InlineData("technique", ClassMechanic.Techniques)]
    [InlineData("tactic", ClassMechanic.Tactics)]
    [InlineData("brand", ClassMechanic.BurningBrand)]
    [InlineData("jar", ClassMechanic.HexedJars)]
    [InlineData("spirit", ClassMechanic.SpiritCatcher)]
    [InlineData("quest", ClassMechanic.Quest)]
    [InlineData("light", ClassMechanic.Light)]
    [InlineData("luna", ClassMechanic.ChampionOfLuna)]
    [InlineData("guardian", ClassMechanic.TacticalGuardian)]
    [InlineData("eclipse", ClassMechanic.CircleOfTheEclipse)]
    public void Catalog_MarksEachCommandOfAClassMechanicsGroup(string group, ClassMechanic mechanic)
    {
        var commands = Catalog().Commands.Where(c => c.Group == group).ToList();

        Assert.NotEmpty(commands);
        Assert.All(commands, c => Assert.Equal(mechanic, c.Mechanic));
    }

    [Fact]
    public void Catalog_LeavesEveryonesCommandsUnmarked_AndUsesEveryMechanic()
    {
        var catalog = Catalog();

        Assert.Null(Assert.Single(catalog.Commands, c => c.Path == "inventory show").Mechanic);
        Assert.Null(Assert.Single(catalog.Commands, c => c.Path == "move defy-danger").Mechanic);
        Assert.Equal(Enum.GetValues<ClassMechanic>().Order(), catalog.Commands.Select(c => c.Mechanic).OfType<ClassMechanic>().Distinct().Order());
    }

    private CommandCatalog Catalog()
    {
        using var scope = _provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CommandCatalogBuilder>().Build();
    }

    // The dashboard calls each command by its title rather than its slash command, so a new one needs a [DisplayName]
    [Fact]
    public void Catalog_GivesEveryCommandATitle_UniqueInItsGroup()
    {
        var catalog = Catalog();

        Assert.All(catalog.Commands, c => Assert.False(string.IsNullOrWhiteSpace(c.Title), $"/{c.Path} needs a [DisplayName]"));
        Assert.Equal("Change shape", Assert.Single(catalog.Commands, c => c.Path == "shapeshift into").Title);
        Assert.All(catalog.Commands.GroupBy(c => c.Group), group =>
            Assert.Equal(group.Count(), group.Select(c => c.Title).Distinct().Count()));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DungeonWorldBot.Data;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Gear;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Shapeshifting;
using DungeonWorldBot.Features.Spells;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway.Extensions;

namespace DungeonWorldBot.Tests.Infrastructure;

public class ServiceRegistrationTests
{
    // As Program registers them: the game's shared services, then the bot's own
    private static IServiceCollection Register() =>
        new ServiceCollection().AddCoreServices().AddAttributedServices(typeof(Program).Assembly);

    // Remora's commands build on its Discord client, which has to be added first
    private static IServiceCollection WithDiscord() => new ServiceCollection().AddDiscordGateway(_ => "token");

    [Theory]
    [InlineData(typeof(ICharacterService), typeof(CharacterService), ServiceLifetime.Scoped)]
    [InlineData(typeof(IGameSettingsService), typeof(GameSettingsService), ServiceLifetime.Singleton)]
    [InlineData(typeof(IDiceRoller), typeof(DiceRoller), ServiceLifetime.Singleton)]
    [InlineData(typeof(CreationSessions), typeof(CreationSessions), ServiceLifetime.Singleton)]
    public void TaggedServices_AreRegistered(Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(Register(), d => d.ServiceType == serviceType);

        Assert.Equal(implementationType, descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(ClassMoveAutocomplete))]
    [InlineData(typeof(AnyClassMoveAutocomplete))]
    [InlineData(typeof(RolledClassMoveAutocomplete))]
    [InlineData(typeof(ClassResourceAutocomplete))]
    [InlineData(typeof(SpellAutocomplete))]
    [InlineData(typeof(SubclassAutocomplete))]
    [InlineData(typeof(GearAutocomplete))]
    [InlineData(typeof(GearModeAutocomplete))]
    [InlineData(typeof(GearCounterAutocomplete))]
    [InlineData(typeof(TransformationAutocomplete))]
    [InlineData(typeof(DiceRollResponder))]
    public void RemoraTypes_AreDiscovered(Type implementationType)
    {
        var assembly = typeof(Program).Assembly;
        var services = new ServiceCollection()
            .AddAutocompleteProviders(assembly)
            .AddResponders(assembly);

        Assert.Contains(services, d => d.ImplementationType == implementationType);
    }

    // GameMasterService takes the operation context so the DM check works from buttons too, not only commands
    [Fact]
    public void OperationContext_IsRegisteredByRemora()
    {
        Assert.Contains(WithDiscord().AddDiscordCommands(enableSlash: true), d => d.ServiceType == typeof(IOperationContext));
    }

    [Fact]
    public void CommandErrorReplies_RunOnBothFailures()
    {
        var services = WithDiscord().AddRemoraServices();

        Assert.Contains(services, d => d.ServiceType == typeof(IPreparationErrorEvent) && d.ImplementationType == typeof(CommandErrorReplies));
        Assert.Contains(services, d => d.ServiceType == typeof(IPostExecutionEvent) && d.ImplementationType == typeof(CommandErrorReplies));
    }

    // The dashboard registers the same shared services without Discord, so each has to be buildable from the database,
    // the settings and each other alone
    [Fact]
    public void CoreServices_AllResolve_WithoutDiscord()
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSqlite<DungeonWorldContext>("Data Source=:memory:")
            .AddSqlite<CanvasContext>("Data Source=:memory:")
            .AddCoreServices();
        services.AddBotSettings();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    // The bot's host checks every service when it's built, so a broken registration would stop it at startup
    [Fact]
    public void BotHost_Builds_WithEveryServiceValid()
    {
        using var host = Program.CreateHostBuilder(
        [
            "--REMORA_BOT_TOKEN=test-token",
            "--ConnectionStrings:DungeonWorldConnectionString=Data Source=:memory:",
        ]).Build();
    }
}

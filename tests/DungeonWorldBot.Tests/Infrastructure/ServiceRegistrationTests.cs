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

namespace DungeonWorldBot.Tests.Infrastructure;

public class ServiceRegistrationTests
{
    private static IServiceCollection Register() =>
        new ServiceCollection().AddAttributedServices(typeof(Program).Assembly);

    [Theory]
    [InlineData(typeof(ICharacterService), typeof(CharacterService), ServiceLifetime.Scoped)]
    [InlineData(typeof(IGameSettingsService), typeof(GameSettingsService), ServiceLifetime.Singleton)]
    [InlineData(typeof(IDiceRoller), typeof(DiceRoller), ServiceLifetime.Singleton)]
    [InlineData(typeof(CharacterCreationDialogue), typeof(CharacterCreationDialogue), ServiceLifetime.Scoped)]
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
}

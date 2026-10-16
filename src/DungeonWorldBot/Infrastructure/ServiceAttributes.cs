using Microsoft.Extensions.DependencyInjection;

namespace DungeonWorldBot.Infrastructure;

/// <summary>
/// Marks a class for automatic registration in the DI container. When no service type is given,
/// the class is registered as itself.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public abstract class ServiceAttribute(ServiceLifetime lifetime, Type? serviceType = null) : Attribute
{
    public ServiceLifetime Lifetime { get; } = lifetime;
    public Type? ServiceType { get; } = serviceType;
}

public sealed class ScopedAttribute() : ServiceAttribute(ServiceLifetime.Scoped);
public sealed class ScopedAttribute<TService>() : ServiceAttribute(ServiceLifetime.Scoped, typeof(TService));

public sealed class SingletonAttribute() : ServiceAttribute(ServiceLifetime.Singleton);
public sealed class SingletonAttribute<TService>() : ServiceAttribute(ServiceLifetime.Singleton, typeof(TService));

public sealed class TransientAttribute() : ServiceAttribute(ServiceLifetime.Transient);
public sealed class TransientAttribute<TService>() : ServiceAttribute(ServiceLifetime.Transient, typeof(TService));

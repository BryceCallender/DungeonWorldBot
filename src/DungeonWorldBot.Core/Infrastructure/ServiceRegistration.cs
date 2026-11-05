using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DungeonWorldBot.Infrastructure;

public static class ServiceRegistration
{
    /// <summary>
    /// The game's services, as both the bot and the dashboard use them: every class here tagged with a
    /// <see cref="ServiceAttribute"/>.
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // The clock, so tests can set the time
        services.TryAddSingleton(TimeProvider.System);
        return services.AddAttributedServices(typeof(ServiceRegistration).Assembly);
    }

    /// <summary>
    /// Registers every class in the assembly tagged with a <see cref="ServiceAttribute"/>.
    /// </summary>
    public static IServiceCollection AddAttributedServices(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false }))
        {
            foreach (var attribute in type.GetCustomAttributes<ServiceAttribute>(inherit: false))
            {
                var serviceType = attribute.ServiceType ?? type;
                if (!serviceType.IsAssignableFrom(type))
                    throw new InvalidOperationException($"{type.Name} does not implement {serviceType.Name}.");

                services.Add(new ServiceDescriptor(serviceType, type, attribute.Lifetime));
            }
        }

        return services;
    }
}

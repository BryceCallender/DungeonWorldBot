using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Remora.Commands.Extensions;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Interactivity;
using Remora.Discord.Pagination.Extensions;

namespace DungeonWorldBot.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddRemoraServices(this IServiceCollection services)
    {
        var asm = Assembly.GetEntryAssembly()!;
        services
            .AddDiscordCommands(enableSlash: true)
            .AddCommands()
            .AddCommands(asm)
            .AddAutocompleteProviders(asm)
            .AddResponders(asm)
            .AddPagination();

        return services;
    }

    /// <summary>
    /// Registers every class in the assembly tagged with a <see cref="ServiceAttribute"/>.
    /// </summary>
    public static IServiceCollection AddAttributedServices(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetConcreteTypes())
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

    public static IServiceCollection AddAutocompleteProviders(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetConcreteTypes().Where(t => t.IsAssignableTo(typeof(IAutocompleteProvider))))
            services.AddAutocompleteProvider(type);

        return services;
    }

    public static IServiceCollection AddResponders(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetConcreteTypes().Where(t => t.IsAssignableTo(typeof(IResponder))))
            services.AddResponder(type);

        return services;
    }

    private static IEnumerable<Type> GetConcreteTypes(this Assembly assembly) =>
        assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

    private static IServiceCollection AddCommands(this IServiceCollection collection, Assembly assembly)
    {
        var types = assembly
            .GetExportedTypes()
            .Where(t => t is { IsClass: true, IsNested: false, IsAbstract: false } && t.IsAssignableTo(typeof(CommandGroup)) && !t.IsAssignableTo(typeof(InteractionGroup)));

        var tree = collection.AddCommandTree();
        
        foreach (var type in types)
            tree.WithCommandGroup(type);
        
        return tree.Finish();
    }
}
using System.Reflection;
using DungeonWorldBot.Features.Characters.Creation;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Rolls;
using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Caching.Services;
using Remora.Commands.Extensions;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Autocomplete;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Gateway.Responders;
using Remora.Discord.Interactivity;
using Remora.Discord.Interactivity.Extensions;
using Remora.Discord.Pagination.Extensions;

namespace DungeonWorldBot.Infrastructure;

public static class ServiceExtensions
{
    /// <summary>
    /// How long a server's role list is kept for the DM check. Remora keeps things 30 seconds (10 unused) by default,
    /// which runs out between commands at the table; role changes still update it sooner, through the gateway.
    /// </summary>
    public static readonly TimeSpan RoleListLifetime = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddRemoraServices(this IServiceCollection services)
    {
        var asm = typeof(Program).Assembly;
        services
            // Keeps what Discord sends (like the server's roles, for the DM check) and answers repeat requests from memory,
            // kept up to date by the gateway's events
            .AddDiscordCaching()
            .Configure<CacheSettings>(cache => cache
                .SetAbsoluteExpiration<IReadOnlyList<IRole>>(RoleListLifetime)
                .SetSlidingExpiration<IReadOnlyList<IRole>>(null))
            .AddDiscordCommands(enableSlash: true)
            .AddCommands()
            .AddCommands(asm)
            .AddCondition<RequireGameMasterCondition>()
            .AddPreparationErrorEvent<CommandErrorReplies>()
            .AddPostExecutionEvent<CommandErrorReplies>()
            .AddAutocompleteProviders(asm)
            .AddResponders(asm)
            .AddPagination()
            .AddInteractionGroup<ChangeOutcomeInteractions>()
            .AddInteractionGroup<CharacterCreationInteractions>();

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
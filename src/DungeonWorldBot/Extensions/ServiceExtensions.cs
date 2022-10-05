using System.Reflection;
using DungeonWorldBot.Commands;
using DungeonWorldBot.Interactions;
using Microsoft.Extensions.DependencyInjection;
using Remora.Commands.Extensions;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Interactivity.Extensions;

namespace DungeonWorldBot.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddRemoraServices(this IServiceCollection services)
    {
        var asm = Assembly.GetEntryAssembly()!;
        services
            .AddDiscordCommands(enableSlash: true)
            .AddCommands()
            .AddCommands(asm)
            .AddPostExecutionEvent<PostCommandHandler>()
            .AddInteractivity()
            .AddInteractionGroup<ModalInteractions>()
            .AddInteractionGroup<CharacterInteractions>();


        return services;
    }

    private static IServiceCollection AddCommands(this IServiceCollection collection, Assembly assembly)
    {
        var types = assembly
            .GetExportedTypes()
            .Where(t => t.IsClass && !t.IsNested && !t.IsAbstract && t.IsAssignableTo(typeof(CommandGroup)));

        var tree = collection.AddCommandTree();
        
        foreach (var type in types)
            tree.WithCommandGroup(type);
        
        return tree.Finish();
    }
}
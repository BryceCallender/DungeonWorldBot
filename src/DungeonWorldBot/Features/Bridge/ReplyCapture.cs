using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Results;

namespace DungeonWorldBot.Features.Bridge;

/// <summary>
/// Notes the messages the bot posts (or edits) while running a command for the dashboard, so the dashboard can show
/// what the command said. Commands post through <see cref="IDiscordRestChannelAPI"/> however they reply, which
/// <see cref="AddReplyCapture"/> wraps to see each one.
/// </summary>
public static class ReplyCapture
{
    // Flows with the run's awaits, so only what that command posts is noted, whatever else the bot is doing at the time
    private static readonly AsyncLocal<List<IMessage>?> Current = new();

    /// <summary>
    /// Starts noting what's posted, until <see cref="Stop"/>. Called from the run itself, so it flows into the command.
    /// </summary>
    public static List<IMessage> Start() => Current.Value = [];

    public static void Stop() => Current.Value = null;

    private static void Saw(IMessage message)
    {
        if (Current.Value is not { } seen)
            return;

        lock (seen)
        {
            // An edit, like a dramatic roll's reveal, replaces what was first posted
            var index = seen.FindIndex(m => m.ID == message.ID);
            if (index >= 0)
                seen[index] = message;
            else
                seen.Add(message);
        }
    }

    /// <summary>
    /// Wraps whatever posts the bot's messages (Remora's API, behind its cache) to note them during a run.
    /// </summary>
    public static IServiceCollection AddReplyCapture(this IServiceCollection services)
    {
        var original = services.Last(d => d.ServiceType == typeof(IDiscordRestChannelAPI));
        services.Remove(original);
        services.Add(ServiceDescriptor.Describe(
            typeof(IDiscordRestChannelAPI),
            provider => CapturingChannelAPI.Wrap((IDiscordRestChannelAPI)Create(provider, original)),
            original.Lifetime));
        return services;
    }

    private static object Create(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);

    /// <summary>
    /// Passes every call through, noting the messages created or edited.
    /// </summary>
    public class CapturingChannelAPI : DispatchProxy
    {
        private IDiscordRestChannelAPI _inner = null!;

        public static IDiscordRestChannelAPI Wrap(IDiscordRestChannelAPI inner)
        {
            var proxy = Create<IDiscordRestChannelAPI, CapturingChannelAPI>();
            ((CapturingChannelAPI)(object)proxy)._inner = inner;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            object? result;
            try
            {
                result = targetMethod!.Invoke(_inner, args);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(e.InnerException);
                throw;
            }

            return targetMethod.Name is nameof(IDiscordRestChannelAPI.CreateMessageAsync) or nameof(IDiscordRestChannelAPI.EditMessageAsync)
                   && result is Task<Result<IMessage>> sent
                ? NoteAsync(sent)
                : result;
        }

        private static async Task<Result<IMessage>> NoteAsync(Task<Result<IMessage>> sent)
        {
            var result = await sent;
            if (result.IsSuccess)
                Saw(result.Entity);
            return result;
        }
    }
}

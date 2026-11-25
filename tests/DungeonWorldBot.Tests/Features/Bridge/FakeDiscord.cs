using System.Collections.Concurrent;
using System.Reflection;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Tests.Features.Bridge;

/// <summary>
/// Discord's REST APIs answering from memory, for running real commands without Discord. Every call fails as "not
/// found", except posting a message to a channel, which is kept in <see cref="Posted"/>.
/// </summary>
public class FakeDiscord
{
    public ConcurrentQueue<(Snowflake Channel, IReadOnlyList<IEmbed> Embeds)> Posted { get; } = new();

    public IDiscordRestChannelAPI Channels => Api<IDiscordRestChannelAPI>();

    public IDiscordRestUserAPI Users => Api<IDiscordRestUserAPI>();

    public IDiscordRestGuildAPI Guilds => Api<IDiscordRestGuildAPI>();

    private ulong _nextID = 1_000;

    internal object? Answer(MethodInfo method, object?[]? args)
    {
        if (method.Name == nameof(IDiscordRestChannelAPI.CreateMessageAsync) && args is [Snowflake channel, ..])
        {
            var embeds = args.OfType<Optional<IReadOnlyList<IEmbed>>>().FirstOrDefault();
            var posted = embeds.HasValue ? embeds.Value : [];
            Posted.Enqueue((channel, posted));
            var message = FakeMessage.Create(DiscordSnowflake.New(Interlocked.Increment(ref _nextID)), channel, posted);
            return Task.FromResult(Result<IMessage>.FromSuccess(message));
        }

        return NotFound(method.ReturnType);
    }

    // A failed Result<T> of whatever the call returns, so the bot carries on as it would after a Discord error
    private static object? NotFound(Type returnType)
    {
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            return null;

        var resultType = returnType.GetGenericArguments()[0];
        object failed = Result.FromError(new NotFoundError());
        if (resultType != typeof(Result))
        {
            var fromError = resultType.GetMethods(BindingFlags.Public | BindingFlags.Static).First(m => m.Name == "FromError");
            if (fromError.IsGenericMethod)
                fromError = fromError.MakeGenericMethod(typeof(NotFoundError));
            var arguments = new object?[fromError.GetParameters().Length];
            arguments[0] = new NotFoundError();
            failed = fromError.Invoke(null, arguments)!;
        }

        return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, [failed]);
    }

    private T Api<T>() where T : class
    {
        var proxy = DispatchProxy.Create<T, FakeApi>();
        ((FakeApi)(object)proxy).Discord = this;
        return proxy;
    }

    public class FakeApi : DispatchProxy
    {
        internal FakeDiscord Discord { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Discord.Answer(targetMethod!, args);
    }

    /// <summary>
    /// Just enough of a message for the bot to note what it posted.
    /// </summary>
    public class FakeMessage : DispatchProxy
    {
        private Snowflake _id;
        private Snowflake _channel;
        private IReadOnlyList<IEmbed> _embeds = [];

        public static IMessage Create(Snowflake id, Snowflake channel, IReadOnlyList<IEmbed> embeds)
        {
            var proxy = Create<IMessage, FakeMessage>();
            var fake = (FakeMessage)(object)proxy;
            (fake._id, fake._channel, fake._embeds) = (id, channel, embeds);
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
        {
            "get_ID" => _id,
            "get_ChannelID" => _channel,
            "get_Content" => string.Empty,
            "get_Embeds" => _embeds,
            _ => targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null,
        };
    }
}

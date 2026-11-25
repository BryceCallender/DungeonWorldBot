using System.Text.Json;
using System.Text.Json.Serialization;

namespace DungeonWorldBot.Features.Bridge;

public record ReplyField(string Name, string Value);

/// <summary>
/// One of the bot's embeds, as much of it as the dashboard shows.
/// </summary>
/// <param name="Color">Its color as RGB, like 0xD85A30, if it has one.</param>
public record ReplyEmbed(string? Title, string? Description, IReadOnlyList<ReplyField> Fields, string? Footer, int? Color);

/// <summary>
/// A message the bot posted while running a command for the dashboard.
/// </summary>
public record ReplyMessage(string? Content, IReadOnlyList<ReplyEmbed> Embeds);

/// <summary>
/// What a <see cref="Data.Entities.CommandRequest"/> came to, kept in its Reply: the messages a run posted, or the
/// values the bot suggested.
/// </summary>
public record CommandReply(IReadOnlyList<ReplyMessage> Messages, IReadOnlyList<CatalogChoice> Suggestions)
{
    public static CommandReply Posted(IReadOnlyList<ReplyMessage> messages) => new(messages, []);

    public static CommandReply Suggested(IReadOnlyList<CatalogChoice> suggestions) => new([], suggestions);
}

/// <summary>
/// How long a command asked for from the dashboard waits for the bot.
/// </summary>
public static class CommandRequestRules
{
    /// <summary>
    /// Commands asked for longer ago than this, as while the bot was off, are dropped rather than all landing at once.
    /// </summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(1);

    public const string TooLate = "The bot wasn't around to run it in time. Try again.";
}

/// <summary>
/// The bridge's JSON, worked out when the bot is built rather than by reflection, which trimmed builds turn off.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = false)]
[JsonSerializable(typeof(CommandCatalog))]
[JsonSerializable(typeof(CommandReply))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class BridgeJson : JsonSerializerContext;

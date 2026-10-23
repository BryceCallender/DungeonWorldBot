using System.Text.RegularExpressions;
using DungeonWorldBot.Data.Entities;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// Finds the 2d6 rolls in a channel's history by reading the bot's roll embeds and working out who rolled each one.
/// Feed it messages oldest first; it remembers recent "." messages across batches to match them with older replies.
/// </summary>
public partial class RollHistoryReader
{
    // Old "." rolls weren't replies, so they're matched with the roll message that came soon after
    private static readonly TimeSpan DotRollWindow = TimeSpan.FromMinutes(2);

    private readonly Snowflake _channelID;
    private readonly LinkedList<IMessage> _pendingDotRolls = new();

    public RollHistoryReader(Snowflake channelID)
    {
        _channelID = channelID;
    }

    // ".2d6", ".d20 + 1" as the old responder took them
    [GeneratedRegex(@"^\.\s*\d*\s*[dD]\s*\d")]
    private static partial Regex LegacyDotRoll();

    public IEnumerable<RollRecord> Read(IEnumerable<IMessage> oldestFirst)
    {
        foreach (var message in oldestFirst)
        {
            if (!message.Author.IsBot.OrDefault(false))
            {
                if (IsDotRoll(message.Content))
                    _pendingDotRolls.AddLast(message);

                continue;
            }

            var (embed, dice) = message.Embeds
                .Where(e => e.Title.OrDefault(string.Empty)?.StartsWith("🎲") == true)
                .Select(e => (Embed: e, Dice: MoveDice.FromDescription(e.Description.OrDefault(string.Empty) ?? string.Empty)))
                .FirstOrDefault(d => d.Dice is not null);

            if (embed is null || dice is null || WhoRolled(message) is not { } userID)
                continue;

            var label = RollLabel.Of(embed);

            yield return new RollRecord
            {
                MessageID = message.ID,
                ChannelID = _channelID,
                UserID = userID,
                First = dice.First,
                Second = dice.Second,
                Total = dice.Total,
                Move = label.Move,
                Stat = label.Stat,
            };
        }
    }

    private static bool IsDotRoll(string content) => DiceMessage.TryParse(content) is not null || LegacyDotRoll().IsMatch(content);

    private Snowflake? WhoRolled(IMessage rollMessage)
    {
        // Slash commands
        if (rollMessage.InteractionMetadata.TryGet(out var metadata))
            return metadata.User.ID;

        if (rollMessage.Interaction.TryGet(out var interaction))
            return interaction.User.ID;

        // "." rolls reply to the message that asked for them
        if (rollMessage.ReferencedMessage.TryGet(out var referenced) && referenced is not null)
        {
            Forget(referenced.ID);
            return referenced.Author.ID;
        }

        // Before they were replies, the roll followed the "." message
        var rolledAt = rollMessage.ID.Timestamp;
        while (_pendingDotRolls.First is { } oldest)
        {
            _pendingDotRolls.RemoveFirst();
            if (rolledAt - oldest.Value.ID.Timestamp <= DotRollWindow)
                return oldest.Value.Author.ID;
        }

        return null;
    }

    private void Forget(Snowflake messageID)
    {
        for (var node = _pendingDotRolls.First; node is not null; node = node.Next)
        {
            if (node.Value.ID == messageID)
            {
                _pendingDotRolls.Remove(node);
                return;
            }
        }
    }
}

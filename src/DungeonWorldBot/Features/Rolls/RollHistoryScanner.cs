using DungeonWorldBot.Infrastructure;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <param name="Found">Every 2d6 roll in the channel.</param>
/// <param name="Added">How many of them weren't recorded yet.</param>
public record RollScan(int Found, int Added);

/// <summary>
/// Reads a channel's whole history for the bot's 2d6 rolls and records the ones that are missing. Safe to run again.
/// </summary>
[Scoped]
public class RollHistoryScanner(IDiscordRestChannelAPI channelAPI, IRollStatsService rollStatsService)
{
    // The most messages Discord hands back per request
    private const int PageSize = 100;

    public async Task<Result<RollScan>> ScanAsync(Snowflake channelID, CancellationToken ct = default)
    {
        var reader = new RollHistoryReader(channelID);
        var after = new Snowflake(0);
        var found = 0;
        var added = 0;

        while (true)
        {
            var page = await channelAPI.GetChannelMessagesAsync(channelID, after: after, limit: PageSize, ct: ct);
            if (!page.IsSuccess)
                return Result<RollScan>.FromError(page);

            if (page.Entity.Count == 0)
                break;

            // Discord returns newest first; the reader needs oldest first
            var messages = page.Entity.OrderBy(m => m.ID.Value).ToList();
            var rolls = reader.Read(messages).ToList();

            found += rolls.Count;
            added += await rollStatsService.AddMissingAsync(rolls);
            after = messages[^1].ID;

            if (page.Entity.Count < PageSize)
                break;
        }

        return new RollScan(found, added);
    }
}

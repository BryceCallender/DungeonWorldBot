using System.ComponentModel;
using DungeonWorldBot.Services;
using DungeonWorldBot.Stats;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Commands;

[Group("stats")]
public class StatsCommand : CommandGroup
{
    // Discord rejects embeds with more than 25 fields
    private const int MaxFields = 25;

    // The most messages Discord hands back per request
    private const int PageSize = 100;

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedbackService;
    private readonly ICharacterService _characterService;
    private readonly IRollStatsService _rollStatsService;
    private readonly IDiscordRestChannelAPI _channelAPI;

    public StatsCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IRollStatsService rollStatsService,
        IDiscordRestChannelAPI channelAPI)
    {
        _context = context;
        _feedbackService = feedbackService;
        _characterService = characterService;
        _rollStatsService = rollStatsService;
        _channelAPI = channelAPI;
    }

    [Command("show")]
    [Description("Show how everyone's 2d6 rolls have gone: hits, misses, nat 12s and nat 2s")]
    public async Task<IResult> ShowStatsAsync(
        [Description("Only show this player's stats")]
        IUser? user = null)
    {
        var stats = await _rollStatsService.GetStatsAsync();
        if (user is not null)
            stats = stats.Where(s => s.UserID == user.ID).ToList();

        if (stats.Count == 0)
        {
            return await ReplyAsync("🎲 Roll Stats", user is null
                ? "No 2d6 rolls recorded yet. Use /stats scan to count the rolls already in this channel."
                : $"No 2d6 rolls recorded for <@{user.ID}> yet.");
        }

        var characters = (await _characterService.GetCharactersAsync()).ToDictionary(c => c.ID);

        var fields = stats
            .Take(MaxFields)
            .Select(s => (IEmbedField)new EmbedField(
                characters.TryGetValue(s.UserID, out var character) ? character.Name : "Player",
                Describe(s)))
            .ToList();

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "🎲 Roll Stats",
                Description: "Every 2d6 roll made through the bot, counted by total with modifiers.",
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("scan")]
    [Description("Count the rolls already in a channel's history toward roll stats. Safe to run again")]
    public async Task<IResult> ScanChannelAsync(
        [Description("The channel to scan. Defaults to this one")]
        IChannel? channel = null)
    {
        if (!_context.TryGetChannelID(out var currentChannelID))
            throw new NotSupportedException();

        var channelID = channel?.ID ?? currentChannelID;

        var started = await ReplyAsync("🎲 Scanning...", $"Reading <#{channelID}> for old rolls. I'll post here when it's done.");
        if (!started.IsSuccess)
            return started;

        var reader = new RollHistoryReader(channelID);
        var after = new Snowflake(0);
        var found = 0;
        var added = 0;

        while (true)
        {
            var page = await _channelAPI.GetChannelMessagesAsync(channelID, after: after, limit: PageSize, ct: CancellationToken);
            if (!page.IsSuccess)
                return await PostResultAsync(currentChannelID, "🎲 Scan Failed", $"Couldn't read <#{channelID}>: {page.Error.Message}");

            if (page.Entity.Count == 0)
                break;

            // Discord returns newest first; the reader needs oldest first
            var messages = page.Entity.OrderBy(m => m.ID.Value).ToList();
            var rolls = reader.Read(messages).ToList();

            found += rolls.Count;
            added += await _rollStatsService.AddMissingAsync(rolls);
            after = messages[^1].ID;

            if (page.Entity.Count < PageSize)
                break;
        }

        return await PostResultAsync(currentChannelID, "🎲 Scan Complete",
            $"Found {found} 2d6 rolls in <#{channelID}>, {added} of them new. See them with /stats show");
    }

    private static string Describe(PlayerRollStats stats) =>
        $"<@{stats.UserID}> · {stats.Rolls} rolls\n" +
        $"Strong hits (10+): {stats.StrongHits} ({Percent(stats.StrongHits, stats.Rolls)})\n" +
        $"Weak hits (7-9): {stats.WeakHits} ({Percent(stats.WeakHits, stats.Rolls)})\n" +
        $"Misses (6-): {stats.Misses} ({Percent(stats.Misses, stats.Rolls)})\n" +
        $"Nat 12s: {stats.Natural12s} · Nat 2s: {stats.Natural2s}";

    private static string Percent(int count, int total) => $"{(double)count / total:P0}";

    // A long scan can outlast the slash command's 15 minute reply window, so the result is posted as its own message
    private async Task<IResult> PostResultAsync(Snowflake channelID, string title, string description)
    {
        var posted = await _channelAPI.CreateMessageAsync(
            channelID,
            embeds: new[] { new Embed(Title: title, Description: description, Colour: _feedbackService.Theme.Primary) },
            ct: CancellationToken);

        return posted.IsSuccess ? Result.FromSuccess() : Result.FromError(posted);
    }

    private async Task<IResult> ReplyAsync(string title, string description)
    {
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: title,
                Description: description,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }
}

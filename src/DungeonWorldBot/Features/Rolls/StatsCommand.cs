using System.ComponentModel;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

[Group("stats")]
public class StatsCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IRollStatsService rollStatsService,
    IDiscordRestChannelAPI channelAPI,
    RollHistoryScanner scanner)
    : DungeonWorldCommandBase(context, feedbackService)
{
    // Discord rejects embeds with more than 25 fields
    private const int MaxFields = 25;

    [Command("show")]
    [Description("Show how everyone's 2d6 rolls in the game chat have gone: hits, misses, nat 12s, nat 2s, modifiers")]
    public async Task<IResult> ShowStatsAsync(
        [Description("Only show this player's stats")]
        IUser? user = null)
    {
        if (rollStatsService.GameChatChannelID is null)
            return await ReplyWithNoGameChatAsync();

        var stats = await rollStatsService.GetStatsAsync();
        if (user is not null)
            stats = stats.Where(s => s.UserID == user.ID).ToList();

        if (stats.Count == 0)
        {
            return await ReplyAsync("🎲 Roll Stats", user is null
                ? $"No 2d6 rolls recorded in <#{rollStatsService.GameChatChannelID}> yet. Use /stats scan to count the rolls already there."
                : $"No 2d6 rolls recorded for <@{user.ID}> in <#{rollStatsService.GameChatChannelID}> yet.");
        }

        var characters = (await characterService.GetCharactersAsync()).ToDictionary(c => c.ID);

        var fields = stats
            .Take(MaxFields)
            .Select(s => (IEmbedField)new EmbedField(
                characters.TryGetValue(s.UserID, out var character) ? character.Name : "Player",
                Describe(s)))
            .ToList();

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: "🎲 Roll Stats",
                Description: $"Every 2d6 roll made through the bot in <#{rollStatsService.GameChatChannelID}>, counted by total with modifiers.",
                Fields: fields,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("scan")]
    [Description("Count the rolls already in the game chat's history toward roll stats. Safe to run again")]
    public async Task<IResult> ScanChannelAsync(
        [Description("The channel to scan. Defaults to the game chat; only game chat rolls count toward stats")]
        IChannel? channel = null)
    {
        if (!_context.TryGetChannelID(out var currentChannelID))
            throw new NotSupportedException();

        if (rollStatsService.GameChatChannelID is not { } gameChat)
            return await ReplyWithNoGameChatAsync();

        var channelID = channel?.ID ?? gameChat;

        var started = await ReplyAsync("🎲 Scanning...", $"Reading <#{channelID}> for old rolls. I'll post here when it's done.");
        if (!started.IsSuccess)
            return started;

        var scan = await scanner.ScanAsync(channelID, CancellationToken);
        if (!scan.IsSuccess)
            return await PostResultAsync(currentChannelID, "🎲 Scan Failed", $"Couldn't read <#{channelID}>: {scan.Error.Message}");

        var counted = channelID == gameChat
            ? "See them with /stats show"
            : $"They're saved, but only rolls in <#{gameChat}> count toward /stats show";

        return await PostResultAsync(currentChannelID, "🎲 Scan Complete",
            $"Found {scan.Entity.Found} 2d6 rolls in <#{channelID}>, {scan.Entity.Added} of them new. {counted}");
    }

    private static string Describe(PlayerRollStats stats) =>
        $"<@{stats.UserID}> · {stats.Rolls} rolls\n" +
        $"Strong hits (10+): {stats.StrongHits} ({Percent(stats.StrongHits, stats.Rolls)})\n" +
        $"Weak hits (7-9): {stats.WeakHits} ({Percent(stats.WeakHits, stats.Rolls)})\n" +
        $"Misses (6-): {stats.Misses} ({Percent(stats.Misses, stats.Rolls)})\n" +
        $"Nat 12s: {stats.Natural12s} · Nat 2s: {stats.Natural2s}\n" +
        $"Modifiers lifted {stats.Lifted} · sank {stats.Sunk}";

    private static string Percent(int count, int total) => $"{(double)count / total:P0}";

    // A long scan can outlast the slash command's 15 minute reply window, so the result is posted as its own message
    private async Task<IResult> PostResultAsync(Snowflake channelID, string title, string description)
    {
        var posted = await channelAPI.CreateMessageAsync(
            channelID,
            embeds: new[] { new Embed(Title: title, Description: description, Colour: _feedbackService.Theme.Primary) },
            ct: CancellationToken);

        return posted.IsSuccess ? Result.FromSuccess() : Result.FromError(posted);
    }

    private async Task<Result> ReplyWithNoGameChatAsync()
    {
        return (Result)await _feedbackService.SendContextualErrorAsync(
            "Roll stats only count rolls in the game chat, and no game chat channel is set. Add a GameChatChannelId setting with the channel's ID.",
            ct: CancellationToken);
    }
}

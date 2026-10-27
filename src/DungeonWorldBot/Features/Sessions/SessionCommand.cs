using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Sessions;

/// <summary>
/// Marking when a game night starts and ends, so the dashboard counts its rolls as one session, and the table gets a
/// recap of how the night went. Anyone at the table can do either.
/// </summary>
[Group("session")]
public class SessionCommand : DungeonWorldCommandBase
{
    private readonly ISessionService _sessions;
    private readonly ICharacterService _characterService;
    private readonly IRollStatsService _rollStatsService;
    private readonly RollHistoryScanner _scanner;
    private readonly IDiscordRestChannelAPI _channelAPI;

    public SessionCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ISessionService sessions,
        ICharacterService characterService,
        IRollStatsService rollStatsService,
        RollHistoryScanner scanner,
        IDiscordRestChannelAPI channelAPI)
        : base(context, feedbackService)
    {
        _sessions = sessions;
        _characterService = characterService;
        _rollStatsService = rollStatsService;
        _scanner = scanner;
        _channelAPI = channelAPI;
    }

    [Command("start")]
    [Description("Start tonight's session. Rolls from now until /session end count as one game night")]
    public async Task<IResult> StartAsync(
        [Description("What tonight's about, e.g. Into the Sunken Abbey")]
        string? title = null)
    {
        if (await _sessions.GetCurrentAsync() is { } current)
            return await ReplyWithErrorAsync(
                $"Session {await _sessions.NumberOfAsync(current)} has been going since {Stamp(current.StartedAt)}. End it with /session end first.");

        if (await _sessions.StartAsync(title) is not { } session)
            return await ReplyWithErrorAsync("Couldn't start the session. Try again.");

        var number = await _sessions.NumberOfAsync(session);
        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: Truncate(session.Title is { } t ? $"📜 Session {number}: {t}" : $"📜 Session {number} begins", 256),
                Description: Truncate($"Started by <@{GetUserId()}>. Gather round, adventurers. Every roll from here counts toward tonight's tale."
                    + await PreviouslyAsync(session), 4096),
                Timestamp: session.StartedAt,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("end")]
    [Description("End tonight's session, with a recap of how the dice went")]
    public async Task<IResult> EndAsync()
    {
        if (await _sessions.EndAsync() is not { } session)
            return await ReplyWithErrorAsync("No session is going on. Start one with /session start.");

        var number = await _sessions.NumberOfAsync(session);
        var recap = await _sessions.RecapAsync(session);
        var names = (await _characterService.GetCharactersAsync()).ToDictionary(c => c.ID, c => c.Name);

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: Truncate(session.Title is { } t ? $"🌙 Session {number} ends: {t}" : $"🌙 Session {number} ends", 256),
                Description: Describe(session, recap),
                Fields: recap.Players
                    // Discord rejects embeds with more than 25 fields
                    .Take(25)
                    .Select(p => (IEmbedField)new EmbedField(
                        // Whoever rolled without a character, like the DM
                        names.GetValueOrDefault(p.UserID) ?? "No character",
                        $"<@{p.UserID}> · {p.Rolls} roll{(p.Rolls == 1 ? "" : "s")}" + (p.Misses > 0 ? $" · {p.Misses} miss{(p.Misses == 1 ? "" : "es")}" : ""),
                        IsInline: true))
                    .ToList(),
                Timestamp: session.EndedAt!.Value,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    [Command("rename")]
    [Description("Rename a session. Defaults to the one going on now, or the last one if none is")]
    public async Task<IResult> RenameAsync(
        [Description("Its new title, e.g. Into the Sunken Abbey")]
        string title,
        [Description("Which session, counting from 1")]
        [MinValue(1)]
        int? number = null)
    {
        var all = await _sessions.GetAllAsync();
        var session = number is { } n
            ? all.ElementAtOrDefault(n - 1)
            : await _sessions.GetCurrentAsync() ?? all.LastOrDefault();
        if (session is null)
            return await ReplyWithErrorAsync(number is null ? "There are no sessions yet. Start one with /session start." : $"There's no session {number}.");

        var before = session.Title;
        await _sessions.RenameAsync(session, title);
        var name = $"Session {await _sessions.NumberOfAsync(session)}";
        return await ReplyAsync("📜 Session Renamed", session.Title is { } t
            ? $"{name}{(before is null ? "" : $" ({before})")} is now **{t}**."
            : $"{name} no longer has a title.");
    }

    [Command("scan")]
    [Description("Add past game nights, guessed from the game chat's rolls. Safe to run again")]
    public async Task<IResult> ScanAsync()
    {
        if (!_context.TryGetChannelID(out var currentChannelID))
            throw new NotSupportedException();

        if (_rollStatsService.GameChatChannelID is not { } gameChat)
            return await ReplyWithErrorAsync("No game chat channel is set. Add a GameChatChannelId setting with the channel's ID.");

        var started = await ReplyAsync("📜 Scanning...", $"Reading <#{gameChat}> for past game nights. I'll post here when it's done.");
        if (!started.IsSuccess)
            return started;

        var scan = await _scanner.ScanAsync(gameChat, CancellationToken);
        if (!scan.IsSuccess)
            return await PostResultAsync(currentChannelID, "📜 Scan Failed", $"Couldn't read <#{gameChat}>: {scan.Error.Message}");

        var added = await _sessions.BackfillAsync();
        if (added.Count == 0)
            return await PostResultAsync(currentChannelID, "📜 Scan Complete", "No game nights were missing. Every night with rolls is already a session.");

        var lines = new List<string>();
        foreach (var session in added)
            lines.Add($"Session {await _sessions.NumberOfAsync(session)} · <t:{session.StartedAt.ToUnixTimeSeconds()}:D> · {(await _sessions.RecapAsync(session)).Rolls} rolls");

        return await PostResultAsync(currentChannelID, "📜 Scan Complete", Truncate(
            $"Added {added.Count} past game night{(added.Count == 1 ? "" : "s")} from <#{gameChat}>, and recorded {scan.Entity.Added} roll{(scan.Entity.Added == 1 ? "" : "s")} that were missing. "
            + "Sessions are renumbered by when they started.\n\n" + string.Join('\n', lines), 4096));
    }

    // A long scan can outlast the slash command's 15 minute reply window, so the result is posted as its own message
    private async Task<IResult> PostResultAsync(Snowflake channelID, string title, string description)
    {
        var posted = await _channelAPI.CreateMessageAsync(
            channelID,
            embeds: new[] { new Embed(Title: title, Description: description, Colour: _feedbackService.Theme.Primary) },
            ct: CancellationToken);

        return posted.IsSuccess ? Result.FromSuccess() : Result.FromError(posted);
    }

    // "Previously, in Session 11: The Ford", with how the dice went and the fights, to remind everyone where they were
    private async Task<string> PreviouslyAsync(GameSession session)
    {
        if (await _sessions.GetPreviousAsync(session) is not { } previous)
            return "";

        var recap = await _sessions.RecapAsync(previous);
        var name = $"Session {await _sessions.NumberOfAsync(previous)}" + (previous.Title is { } t ? $": {t}" : "");
        var lines = new List<string> { $"\n\n**Previously, in {name}…**" };
        if (recap.Fights.Count > 0)
            lines.Add($"⚔️ {string.Join(", ", recap.Fights)}");
        if (recap.Rolls > 0)
            lines.Add($"🎲 {recap.Rolls} roll{(recap.Rolls == 1 ? "" : "s")} · {recap.Hits * 100 / recap.Rolls}% hit"
                + (recap.Natural12s > 0 ? $" · ✨ {recap.Natural12s} natural 12{(recap.Natural12s == 1 ? "" : "s")}" : ""));
        return string.Join('\n', lines);
    }

    private static string Describe(GameSession session, SessionRecap recap)
    {
        var length = session.EndedAt!.Value - session.StartedAt;
        var played = length.TotalHours >= 1 ? $"{(int)length.TotalHours}h {length.Minutes}m" : $"{Math.Max(1, length.Minutes)}m";
        if (recap.Rolls == 0)
            return $"Played for {played}. Nobody rolled 2d6 in the game chat.";

        var lines = new List<string>
        {
            $"Played for **{played}** · **{recap.Rolls}** roll{(recap.Rolls == 1 ? "" : "s")} · **{recap.Hits * 100 / recap.Rolls}%** hit",
            $"💀 {recap.Misses} miss{(recap.Misses == 1 ? "" : "es")}",
        };
        if (recap.Natural12s > 0 || recap.Natural2s > 0)
            lines.Add($"✨ {recap.Natural12s} natural 12{(recap.Natural12s == 1 ? "" : "s")} · 🐍 {recap.Natural2s} snake eyes");
        if (recap.Fights.Count > 0)
            lines.Add($"⚔️ {string.Join(", ", recap.Fights)}");
        return string.Join('\n', lines);
    }

    // Discord shows it in each reader's own time zone, e.g. "7:02 PM"
    private static string Stamp(DateTimeOffset at) => $"<t:{at.ToUnixTimeSeconds()}:t>";
}

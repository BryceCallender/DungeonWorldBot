using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure;
using Microsoft.Extensions.Logging;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// Posts a roll, slowly when it's dramatic (see <see cref="DramaticRolls"/>): the dice "roll" first, then the message is
/// edited a moment later to show how it landed. Every way of rolling 2d6 goes through it, so they all feel the same.
/// </summary>
[Scoped]
public class RollReveal(
    IDiscordRestChannelAPI channelAPI,
    IGameSettingsService settings,
    IRollStatsService rollStats,
    ILogger<RollReveal> logger)
{
    /// <summary>
    /// The drama in a roll about to be posted, or null when there's none or the DM turned it off. Ask before recording
    /// the roll, so a comeback counts the misses before it.
    /// </summary>
    /// <param name="outcome">What it counts as, when something (like Tactics) changed it from what the dice say.</param>
    /// <param name="health">Their character's health, if they rolled as one.</param>
    public async Task<DramaticRoll?> DramaOfAsync(Snowflake roller, DiceResult roll, string? move, MoveOutcome? outcome = null, Health? health = null)
    {
        if (!settings.DramaticRolls || MoveDice.From(roll) is not { } dice)
            return null;

        var counted = outcome ?? dice.Outcome;
        // Only a strong hit can be a comeback, so the misses are only counted for one
        var misses = counted == MoveOutcome.StrongHit ? await rollStats.MissStreakAsync(roller) : 0;
        return DramaticRolls.For(dice, move, counted, misses, health);
    }

    /// <summary>
    /// Posts the roll: straight away, or the teaser then the roll landing when it's dramatic.
    /// </summary>
    /// <param name="who">Who's rolling, for the teaser.</param>
    /// <param name="what">What they're rolling, like the move, for the teaser.</param>
    /// <param name="send">Posts an embed where the roll goes: a reply, the command's response, or game chat.</param>
    /// <returns>The message the roll ended up in, to record it against.</returns>
    public async Task<Result<IMessage>> SendAsync(Embed roll, DramaticRoll? drama, string who, string? what,
        Func<Embed, Task<Result<IMessage>>> send, CancellationToken ct = default)
    {
        if (drama is null)
            return await send(roll);

        var revealed = drama.Reveal(roll);
        var teaser = await send(drama.Teaser(who, what));
        if (!teaser.IsSuccess)
            return await send(revealed);

        // Not cut short with the command, or the teaser would be left hanging
        await Task.Delay(drama.Pause, CancellationToken.None);

        var edited = await channelAPI.EditMessageAsync(teaser.Entity.ChannelID, teaser.Entity.ID, embeds: new[] { revealed }, ct: ct);
        if (edited.IsSuccess)
            return Result<IMessage>.FromSuccess(edited.Entity);

        // Better posted twice than not at all
        logger.LogWarning("Couldn't land a dramatic roll, so it's posted again: {Reason}", edited.Error.Message);
        await channelAPI.DeleteMessageAsync(teaser.Entity.ChannelID, teaser.Entity.ID, ct: ct);
        return await send(revealed);
    }
}

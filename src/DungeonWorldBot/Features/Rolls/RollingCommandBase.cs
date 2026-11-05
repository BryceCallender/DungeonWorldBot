using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Infrastructure;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

/// <summary>
/// Commands for moves rolled outside /move, like a subclass's or a shapeshifter's: they roll 2d6 + a stat the same way,
/// reply the same way, and count toward roll stats as /move's rolls do.
/// </summary>
public abstract class RollingCommandBase(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    StatRoller statRoller,
    IRollStatsService rollStatsService)
    : DungeonWorldCommandBase(context, feedbackService)
{
    protected const string BonusDescription = "Any +forward, ongoing bonus, or penalty to add to the roll";

    protected ICharacterService Characters { get; } = characterService;

    /// <returns>The character of whoever ran the command, or why there's none to use.</returns>
    protected async Task<Result<Character>> RequireOwnCharacterAsync() =>
        await Characters.GetCharacterFromUserAsync(GetUserId()) is { } character
            ? character
            : new InvalidOperationError(NoCharacterMessage);

    protected Task<StatRoll> RollStatAsync(Character character, StatType stat, int bonus) =>
        statRoller.RollAsync(character, stat, bonus);

    /// <param name="move">What roll stats counts it as, when it isn't the embed's title.</param>
    protected async Task<IResult> ReplyWithRollAsync(
        Character character,
        string title,
        string flavor,
        MoveRoll roll,
        IReadOnlyList<IEmbedField> fields,
        string? move = null)
    {
        var embed = new Embed(
            Title: title,
            Description: $"{flavor}\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
            Fields: fields.ToList(),
            Colour: MoveRoller.ColorFor(roll.Outcome));

        var reply = await ReplyWithEmbedAsync(embed);
        if (!reply.IsSuccess || !_context.TryGetChannelID(out var channelID))
            return reply;

        var label = RollLabel.Of(embed);
        await rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, roll.Dice, move is null ? label : label with { Move = move });
        return reply;
    }
}

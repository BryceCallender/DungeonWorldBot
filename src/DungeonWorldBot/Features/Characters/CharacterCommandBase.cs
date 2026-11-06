using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Features.Leveling;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Characters;

/// <summary>
/// What /character, /character level, and the DM's /dm stat and /dm level commands share: finding characters and
/// replying about levels and stat points.
/// </summary>
public abstract class CharacterCommandBase(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    ILevelingService leveling,
    IGameSettingsService settings)
    : DungeonWorldCommandBase(context, feedbackService)
{
    protected readonly ICharacterService _characterService = characterService;
    protected readonly ILevelingService _leveling = leveling;
    protected readonly IGameSettingsService _settings = settings;

    protected Task<Character?> GetCharacterAsync(IUser? user) => _characterService.GetCharacterFromUserAsync(TargetOf(user));

    protected async Task<Character?> GetOwnCharacterAsync() =>
        await _characterService.GetCharacterFromUserAsync(GetUserId());

    protected async Task<IResult> ReplyWithLevelPointsAsync(string title)
    {
        var table = _settings.GetStatPointTable();
        var description = $"Every level gives **{table.Default}** stat point{(table.Default == 1 ? "" : "s")}";
        description += table.PerLevel.Count == 0
            ? "."
            : ", except:\n" + string.Join('\n', table.PerLevel.OrderBy(o => o.Key).Select(o => $"• Level {o.Key}: **{o.Value}**"));
        description += "\n\nPoints are given when a character levels up, for every character. Changes don't affect levels already gained; the DM can use /dm stat grant-points for those.";

        return await ReplyWithEmbedAsync(
            new Embed(
                Title: title,
                Description: description,
                Colour: _feedbackService.Theme.Primary
            )
        );
    }

    protected async Task<IResult> ReplyWithLevelChangedAsync(Character character)
    {
        return await ReplyWithEmbedAsync(
            new Embed(
                Title: "Level Changed!",
                Description: $"{character.Name} is now level {character.Level}."
                             + (ClassSheetLibrary.For(character.Class.Type) is not null && character.Level >= 2
                                 ? "\nPick a new advanced move with /class moves and /class learn"
                                 : string.Empty)
                             + SubclassUnlocks(character)
                             + (_leveling.GetStatPoints(character) is > 0 and var points
                                 ? $"\nRaise a stat with /character level allocate ({points} point{(points == 1 ? "" : "s")} to spend)"
                                 : string.Empty),
                Colour: _feedbackService.Theme.Primary
            )
        );
    }

    /// <summary>
    /// The subclass moves that unlock at the character's new level, if any.
    /// </summary>
    private static string SubclassUnlocks(Character character)
    {
        if (SubclassLibrary.Of(character) is not { } subclass)
            return string.Empty;

        var unlocked = subclass.Moves.Where(m => m.Level == character.Level).ToList();
        if (unlocked.Count == 0)
            return string.Empty;

        return "\n" + string.Join("\n", unlocked.Select(m => m.Hidden
            ? $"{subclass.Name}: something stirs... ▓▒░ ??? ░▒▓ Ask your DM."
            : $"{subclass.Name}: learned **{m.Move.Name}**. See /subclass info"));
    }
}

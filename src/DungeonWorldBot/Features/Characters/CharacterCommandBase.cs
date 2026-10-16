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
public abstract class CharacterCommandBase : DungeonWorldCommandBase
{
    protected readonly ICharacterService _characterService;
    protected readonly IGameMasterService _gameMaster;
    protected readonly ILevelingService _leveling;
    protected readonly IGameSettingsService _settings;

    protected CharacterCommandBase(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IGameMasterService gameMaster,
        ILevelingService leveling,
        IGameSettingsService settings)
        : base(context, feedbackService)
    {
        _characterService = characterService;
        _gameMaster = gameMaster;
        _leveling = leveling;
        _settings = settings;
    }

    protected async Task<Character?> GetCharacterAsync(IUser? user) =>
        user is null ? await GetOwnCharacterAsync() : await _characterService.GetCharacterFromUserAsync(user.ID);

    protected async Task<Character?> GetOwnCharacterAsync() =>
        await _characterService.GetCharacterFromUserAsync(GetUserId());

    protected static string FormatModifier(int modifier) => modifier >= 0 ? $"+{modifier}" : modifier.ToString();

    protected async Task<Result> ReplyNotGameMasterAsync(string? hint = null)
    {
        var message = $"Only someone with the {_gameMaster.RoleName} role can do that.";
        return await ReplyWithErrorAsync(hint is null ? message : $"{message} {hint}");
    }

    protected async Task<IResult> ReplyWithLevelPointsAsync(string title)
    {
        var (perLevel, overrides) = _settings.GetStatPointTable();
        var description = $"Every level gives **{perLevel}** stat point{(perLevel == 1 ? "" : "s")}";
        description += overrides.Count == 0
            ? "."
            : ", except:\n" + string.Join('\n', overrides.OrderBy(o => o.Key).Select(o => $"• Level {o.Key}: **{o.Value}**"));
        description += "\n\nPoints are given when a character levels up, for every character. Changes don't affect levels already gained; the DM can use /dm stat grant-points for those.";

        return await _feedbackService.SendContextualEmbedAsync(
            new Embed(
                Title: title,
                Description: description,
                Colour: _feedbackService.Theme.Primary
            ),
            ct: CancellationToken
        );
    }

    protected async Task<IResult> ReplyWithLevelChangedAsync(Character character)
    {
        return await _feedbackService.SendContextualEmbedAsync(
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
            ),
            ct: CancellationToken
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

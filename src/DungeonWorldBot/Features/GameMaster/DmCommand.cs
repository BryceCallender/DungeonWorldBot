using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Leveling;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.GameMaster;

/// <summary>
/// Commands only the DM can use. Discord can only hide whole top-level commands from people, so they live together
/// here: hidden from anyone without Manage Server (a server admin can let the DM role see them under Server Settings →
/// Integrations), and still checked against the DM role when run.
/// </summary>
[Group("dm")]
[DiscordDefaultMemberPermissions(DiscordPermission.ManageGuild)]
public partial class DmCommand : CommandGroup
{
    private const string ClearHint = "Type \"none\" to clear it.";

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// The new value of a text field: unchanged when left blank, cleared by "none".
    /// </summary>
    private static string? Change(string? current, string? value) =>
        value is null ? current
        : value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase) ? null
        : Text(value) ?? current;

    [Group("stat")]
    public class StatCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IGameMasterService gameMaster,
        ILevelingService leveling,
        IGameSettingsService settings)
        : CharacterCommandBase(context, feedbackService, characterService, gameMaster, leveling, settings)
    {
        [Command("set")]
        [Description("Set any character's stat to a value")]
        public async Task<IResult> ChangeStatAsync(
            [Description("The stat to change")]
            StatType type,
            [Description("The new value of the stat")]
            int value,
            [Description("The player whose character to change. Leave blank for your own.")]
            IUser? user = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync("Players raise stats with /character level allocate when they level up.");

            var character = await GetCharacterAsync(user);
            if (character is null)
            {
                return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");
            }

            await _characterService.UpdateCharacterStatAsync(character, type, value);

            return await _feedbackService.SendContextualEmbedAsync(
                new Embed(
                    Title: "Stat Changed!",
                    Description: $"{character.Name}'s {type} was changed to {value}",
                    Colour: _feedbackService.Theme.Primary
                ),
                ct: CancellationToken
            );
        }

        [Command("grant-points")]
        [Description("Give a character stat points to spend")]
        public async Task<IResult> GrantStatPointsAsync(
            [Description("The player whose character gets the points")]
            IUser user,
            [Description("How many points")]
            [MinValue(1)]
            int amount = 1)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            var character = await _characterService.GetCharacterFromUserAsync(user.ID);
            if (character is null)
            {
                return await ReplyWithErrorAsync($"{user.Username} does not have a character.");
            }

            await _leveling.GrantStatPointsAsync(character, amount);

            return await _feedbackService.SendContextualEmbedAsync(
                new Embed(
                    Title: "Stat Points Granted!",
                    Description: $"{character.Name} now has {_leveling.GetStatPoints(character)} stat point(s) to spend with /character level allocate",
                    Colour: _feedbackService.Theme.Primary
                ),
                ct: CancellationToken
            );
        }
    }

    [Group("level")]
    public class LevelCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IGameMasterService gameMaster,
        ILevelingService leveling,
        IGameSettingsService settings)
        : CharacterCommandBase(context, feedbackService, characterService, gameMaster, leveling, settings)
    {
        [Command("set")]
        [Description("Set a character's level, or everyone's")]
        public async Task<IResult> SetLevelAsync(
            [Description("The new level")]
            [MinValue(1)]
            int level,
            [Description("The player whose character to change. Leave blank for your own.")]
            IUser? user = null,
            [Description("Set every character to this level")]
            bool everyone = false,
            [Description("Stat points for reaching this level. Leave blank to use the amount from /dm level set-points")]
            [MinValue(0)]
            int? points = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync("Only the DM can change levels.");

            // Saved like /dm level set-points, so anyone who reaches this level later gets the same
            if (points is { } levelPoints)
            {
                if (level < 2)
                    return await ReplyWithErrorAsync("Level 1 doesn't give stat points.");

                await _settings.SetStatPointsForLevelAsync(level, levelPoints);
            }

            if (everyone)
            {
                var characters = await _characterService.GetCharactersAsync();
                foreach (var each in characters)
                {
                    await _leveling.ChangeLevelAsync(each, level);
                }

                return await _feedbackService.SendContextualEmbedAsync(
                    new Embed(
                        Title: "Party Level Changed!",
                        Description: $"Every character is now level {level}: {string.Join(", ", characters.Select(c => c.Name))}\n" +
                                     "Anyone who went up a level has stat points to spend with /character level allocate",
                        Colour: _feedbackService.Theme.Primary
                    ),
                    ct: CancellationToken
                );
            }

            var character = await GetCharacterAsync(user);
            if (character is null)
            {
                return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");
            }

            await _leveling.ChangeLevelAsync(character, level);

            return await ReplyWithLevelChangedAsync(character);
        }

        [Command("set-points")]
        [Description("Set how many stat points a level gives everyone")]
        public async Task<IResult> SetLevelPointsAsync(
            [Description("Stat points gained for reaching the level")]
            [MinValue(0)]
            int points,
            [Description("The level this applies to. Leave blank to set every level")]
            [MinValue(2)]
            int? level = null)
        {
            if (!await _gameMaster.IsGameMasterAsync(CancellationToken))
                return await ReplyNotGameMasterAsync();

            if (level is { } only)
                await _settings.SetStatPointsForLevelAsync(only, points);
            else
                await _settings.SetDefaultStatPointsAsync(points);

            return await ReplyWithLevelPointsAsync("Stat Points Updated!");
        }
    }
}

using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Debilities;

[Group("debility")]
public class DebilityCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IDebilityService debilityService,
    IGameMasterService gameMaster)
    : DungeonWorldCommandBase(context, feedbackService)
{
    private const string UserDescription = "DM only: the player whose character to change. Leave blank for your own.";

    [Command("add")]
    [Description("Mark a debility: -1 to rolls with its stat until it's removed")]
    public async Task<IResult> AddAsync(
        [Description("Weak (STR), Shaky (DEX), Sick (CON), Stunned (INT), Confused (WIS) or Scarred (CHA)")]
        DebilityType debility,
        [Description(UserDescription)]
        IUser? user = null)
    {
        var (character, error) = await FindCharacterAsync(user);
        if (character is null)
            return error!;

        if (!await debilityService.AddAsync(character, debility))
            return await ReplyWithErrorAsync($"{character.Name} is already {debility}.");

        var description = $"{character.Name} is now **{DebilityRules.Describe(debility)}**: -1 to rolls with {DebilityRules.StatFor(debility).ToString().ToUpper()}.";
        if (DebilityRules.IgnoringMove(character, DebilityRules.StatFor(debility)) is { } ignoring)
            description += $"\n*{ignoring.Name} lets them ignore the -1.*";

        var all = DebilityRules.Current(character);
        if (all.Count == Enum.GetValues<DebilityType>().Length)
            description += "\n\n**They have all six debilities.** Moves that would give another (like Withdrawal or Bloody Aegis) can't.";

        return await ReplyAsync("Debility Marked", $"{description}\n\n{Summary(character)}");
    }

    [Command("remove")]
    [Description("Clear one debility")]
    public async Task<IResult> RemoveAsync(
        [Description("The debility to clear")]
        DebilityType debility,
        [Description(UserDescription)]
        IUser? user = null)
    {
        var (character, error) = await FindCharacterAsync(user);
        if (character is null)
            return error!;

        if (!await debilityService.RemoveAsync(character, debility))
            return await ReplyWithErrorAsync($"{character.Name} isn't {debility}.");

        return await ReplyAsync("Debility Cleared", $"{character.Name} is no longer {debility}.\n\n{Summary(character)}");
    }

    [Command("clear")]
    [Description("Clear every debility, e.g. after recovering")]
    public async Task<IResult> ClearAsync(
        [Description(UserDescription)]
        IUser? user = null)
    {
        var (character, error) = await FindCharacterAsync(user);
        if (character is null)
            return error!;

        var removed = await debilityService.ClearAsync(character);
        return await ReplyAsync("Debilities Cleared", removed.Count > 0
            ? $"{character.Name} is no longer {string.Join(", ", removed)}."
            : $"{character.Name} had no debilities.");
    }

    [Command("show")]
    [Description("Show a character's debilities")]
    public async Task<IResult> ShowAsync(
        [Description("The user to see the debilities of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = user is not null
            ? await characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        return await ReplyAsync($"{character.Name}'s Debilities", Summary(character));
    }

    /// <summary>
    /// Every debility the character has and what it does, or that they have none.
    /// </summary>
    public static string Summary(Character character)
    {
        var current = DebilityRules.Current(character);
        if (current.Count == 0)
            return "No debilities.";

        return string.Join('\n', current.Select(d =>
        {
            var stat = DebilityRules.StatFor(d);
            var ignored = DebilityRules.IgnoringMove(character, stat);
            return $"• **{DebilityRules.Describe(d)}**: " + (ignored is null ? $"-1 to {stat.ToString().ToUpper()} rolls" : $"ignored ({ignored.Name})");
        }));
    }

    /// <returns>The character, or null and an error already sent (changing someone else's needs the DM role).</returns>
    private async Task<(Character?, IResult?)> FindCharacterAsync(IUser? user)
    {
        if (user is not null && _context.TryGetUserID(out var self) && user.ID != self && !await gameMaster.IsGameMasterAsync(CancellationToken))
            return (null, await ReplyWithErrorAsync($"Only someone with the {gameMaster.RoleName} role can change someone else's debilities."));

        var character = user is not null
            ? await characterService.GetCharacterFromUserAsync(user.ID)
            : await GetOwnCharacterAsync();

        if (character is null)
            return (null, await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character."));

        return (character, null);
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await characterService.GetCharacterFromUserAsync(GetUserId());
}

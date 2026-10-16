using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.GameMaster;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Combat;

/// <summary>
/// Physical and magical armor, and temporary armor from moves like Guardian's Stand or a Lightning barrier.
/// Damage taken with /character damage or /subclass guardian take-hit is reduced by the armor matching its type.
/// </summary>
[Group("armor")]
public class ArmorCommand : DungeonWorldCommandBase
{
    private const string UserDescription = "The player whose character to change. Leave blank for your own.";
    private const string DefaultSource = "Bonus armor";

    private readonly ICharacterService _characterService;
    private readonly IArmorService _armorService;
    private readonly IGameMasterService _gameMaster;

    public ArmorCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IArmorService armorService,
        IGameMasterService gameMaster)
        : base(context, feedbackService)
    {
        _characterService = characterService;
        _armorService = armorService;
        _gameMaster = gameMaster;
    }

    [Command("show")]
    [Description("Show a character's physical and magical armor, and any temporary armor")]
    public async Task<IResult> ShowAsync(
        [Description("The user to see the armor of. Don't specify a user to see your own.")]
        IUser? user = null)
    {
        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        return await ReplyAsync($"{character.Name}'s Armor", DamageRules.Summary(character));
    }

    [Command("set")]
    [Description("Set your own physical or magical armor, e.g. after changing gear (the DM can set anyone's)")]
    public async Task<IResult> SetAsync(
        [Description("Physical armor stops physical damage, magical armor stops magical damage")]
        ArmorType type,
        [Description("The new armor value, before temporary armor")]
        [MinValue(0)]
        int value,
        [Description(UserDescription)]
        IUser? user = null)
    {
        if (user is not null && _context.TryGetUserID(out var self) && user.ID != self && !await _gameMaster.IsGameMasterAsync(CancellationToken))
            return await ReplyWithErrorAsync($"Only someone with the {_gameMaster.RoleName} role can set someone else's armor.");

        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        var before = DamageRules.BaseArmor(character, type);
        await _armorService.SetArmorAsync(character, type, value);

        return await ReplyAsync("Armor Changed",
            $"{character.Name}'s {DamageRules.Describe(type)} armor: {before} → **{value}**\n\n{DamageRules.Summary(character)}");
    }

    [Command("add")]
    [Description("Give temporary armor, like Guardian's Stand or a Lightning barrier")]
    public async Task<IResult> AddAsync(
        [Description("How much armor. Negative lowers it, e.g. sundered armor")]
        int amount,
        [Description("Physical armor stops physical damage, magical armor stops magical damage")]
        ArmorType type = ArmorType.Physical,
        [Description("Fight: until /armor end-fight. UntilHit: breaks on the next hit. UntilRemoved: until /armor remove")]
        ArmorDuration duration = ArmorDuration.Fight,
        [Description("Where it comes from, e.g. Guardian's Stand. Use the same name to remove it later")]
        string source = DefaultSource,
        [Description(UserDescription)]
        IUser? user = null,
        [Description("Give it to every character instead, e.g. a barrier around the whole party")]
        bool party = false)
    {
        if (amount == 0)
            return await ReplyWithErrorAsync("Add at least 1 armor (or a negative amount to lower it).");

        List<Character> characters;
        if (party)
        {
            characters = await _characterService.GetCharactersAsync();
        }
        else
        {
            var character = await GetCharacterAsync(user);
            if (character is null)
                return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");
            characters = [character];
        }

        source = string.IsNullOrWhiteSpace(source) ? DefaultSource : source.Trim();
        foreach (var character in characters)
            await _armorService.AddBonusAsync(character, source, type, amount, duration);

        var bonus = $"{(amount >= 0 ? "+" : "")}{amount} {DamageRules.Describe(type)} armor from {source} ({DamageRules.Describe(duration)})";
        return await ReplyAsync("Armor Added", characters.Count == 1
            ? $"{characters[0].Name} gets {bonus}.\n\n{DamageRules.Summary(characters[0])}"
            : $"Everyone gets {bonus}.\n\n{string.Join('\n', characters.Select(c => $"**{c.Name}**: {DamageRules.Totals(c)}"))}");
    }

    [Command("remove")]
    [Description("Remove temporary armor by its source, or all of it")]
    public async Task<IResult> RemoveAsync(
        [Description("The source it was added with. Leave blank to remove all temporary armor")]
        string? source = null,
        [Description(UserDescription)]
        IUser? user = null)
    {
        var character = await GetCharacterAsync(user);
        if (character is null)
            return await ReplyWithErrorAsync(user is null ? NoCharacterMessage : $"{user.Username} does not have a character.");

        var removed = await _armorService.RemoveBonusesAsync(character, string.IsNullOrWhiteSpace(source) ? null : source);
        if (removed.Count == 0)
        {
            return await ReplyWithErrorAsync(string.IsNullOrWhiteSpace(source)
                ? $"{character.Name} has no temporary armor."
                : $"{character.Name} has no armor from {source.Trim()}. See /armor show");
        }

        return await ReplyAsync("Armor Removed",
            $"Removed {string.Join(", ", removed.Select(DamageRules.Describe))}.\n\n{DamageRules.Summary(character)}");
    }

    [Command("end-fight")]
    [Description("The fight is over: remove everyone's temporary armor that lasted for the fight or until hit")]
    public async Task<IResult> EndFightAsync()
    {
        var cleared = new List<string>();
        foreach (var character in await _characterService.GetCharactersAsync())
        {
            var removed = await _armorService.EndFightAsync(character);
            if (removed.Count > 0)
                cleared.Add($"**{character.Name}**: {string.Join(", ", removed.Select(b => b.Source))}");
        }

        return await ReplyAsync("Fight Over", cleared.Count > 0
            ? $"Removed temporary armor:\n{string.Join('\n', cleared)}"
            : "Nobody had temporary armor to remove.");
    }

    private async Task<Character?> GetCharacterAsync(IUser? user)
    {
        if (user is not null)
            return await _characterService.GetCharacterFromUserAsync(user.ID);

        var userId = GetUserId();

        return await _characterService.GetCharacterFromUserAsync(userId);
    }
}

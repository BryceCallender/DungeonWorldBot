using d20Tek.DiceNotation.Results;
using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace DungeonWorldBot.Features.Rolls;

[Group("roll")]
public class DiceRollCommand : DungeonWorldCommandBase
{
    private readonly ICharacterService _characterService;
    private readonly IDiceRoller _diceRoller;
    private readonly IRollStatsService _rollStatsService;
    private readonly IDamageService _damageService;
    
    public DiceRollCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiceRoller diceRoller,
        IRollStatsService rollStatsService,
        IDamageService damageService)
        : base(context, feedbackService)
    {
        _characterService = characterService;
        _diceRoller = diceRoller;
        _rollStatsService = rollStatsService;
        _damageService = damageService;
    }
    
    [Command("die")]
    [Description("Roll a dice like d6 or 2d6. Defaults to a 2d6")]
    public async Task<IResult> RollDiceAsync(string value = "2d6")
    {
        if (!_diceRoller.TryRoll(value, out var result))
            return await ReplyWithErrorAsync($"`{value}` is not a valid dice roll.");

        return await ReplyWithRoll(result);
    }

    [Command("stat")]
    [Description("Rolls a default 2d6 dice and adds your stat modifier")]
    public async Task<IResult> RollDiceWithStatAsync(StatType statType, string value = "2d6")
    {
        var userID = GetUserId();
    
        var character = await _characterService.GetCharacterFromUserAsync(userID);
    
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to roll on stats. Try using /character create");

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        // Everything that changes the roll, like /move: the stat's debility and subclass bonuses (a Boon of Aptitude or
        // its Echo from a party member)
        var bonuses = new List<RollBonus> { new(statType.ToString().ToUpper(), stat.Modifier) };
        if (DebilityRules.RollPenalty(character, statType) is { } penalty)
            bonuses.Add(new RollBonus(penalty.Debility.ToString(), penalty.Penalty));
        bonuses.AddRange(await _damageService.BonusesAsync(character, RollKind.Stat, statType));

        if (!_diceRoller.TryRoll(DiceHelper.AddModifier(value, bonuses.Sum(b => b.Amount)), out var result))
            return await ReplyWithErrorAsync($"`{value}` is not a valid dice roll.");

        return await ReplyWithRoll(result, statType.ToString(), RolledWith(bonuses));
    }
    
    [Command("damage")]
    [Description("Rolls your respective classes damage dice. Can also add on modifiers as needed.")]
    public async Task<IResult> RollDamageDiceAsync(string modifiers = "")
    {
        var userID = GetUserId();
        
        var character = await _characterService.GetCharacterFromUserAsync(userID);
        
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to roll for damage. Try using /character create");
    
        var damageDice = character.DamageModifier ?? character.Class.Damage;
        var bonuses = await _damageService.DamageBonusesAsync(character);

        // "2", "+2", "-1" or "d4": anything without a sign is added
        modifiers = modifiers.Trim();
        var extra = modifiers.Length > 0 && modifiers[0] is not ('+' or '-') ? $"+{modifiers}" : modifiers;
        var rollString = DiceHelper.AddModifier(damageDice, bonuses.Sum(b => b.Amount)) + extra;
        
        if (!_diceRoller.TryRoll(rollString, out var result))
            return await ReplyWithErrorAsync($"`{rollString}` is not a valid dice roll.");

        // Gear that changes any damage (like a Sapphire reroll) still applies. It isn't for a particular move, so gear
        // only used for some moves doesn't.
        var gear = await _damageService.ApplyGearAsync(character, string.Empty, DamageService.DamageDice(result));

        var parts = bonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}").Prepend(damageDice).ToList();
        if (modifiers.Length > 0)
            parts.Add($"Extra {extra}");

        var details = (gear.Bonus != 0 ? $"\n**{result.Value + gear.Bonus}** damage" : string.Empty)
                      + string.Concat(gear.Notes.Select(n => $"\n{n}"));

        return await ReplyWithRoll(result, "Damage", new EmbedField("Rolled with", string.Join(", ", parts)), details);
    }

    private async Task<IResult> ReplyWithRoll(DiceResult roll)
    {
        return await ReplyWithRoll(roll, null);
    }

    /// <param name="rolledWith">What went into the roll, shown above the outcome.</param>
    /// <param name="details">Extra lines under the dice, like what gear did.</param>
    private async Task<IResult> ReplyWithRoll(DiceResult roll, string? rollDescription, IEmbedField? rolledWith = null, string? details = null)
    {
        var outcome = MoveRoller.OutcomeOf(roll);

        var fields = new List<IEmbedField>();
        if (rolledWith is not null)
            fields.Add(rolledWith);
        if (outcome is { } band)
            fields.Add(new EmbedField(MoveRoller.Describe(band), MoveRoller.GeneralMeaning(band)));

        var embed = new Embed(
            Title: $"🎲 {rollDescription} Roll(s)",
            Description: DiceHelper.BuildDiceResult(roll) + details,
            Fields: fields.Count > 0 ? fields : new Optional<IReadOnlyList<IEmbedField>>(),
            Colour: outcome is { } o ? MoveRoller.ColourFor(o) : _feedbackService.Theme.Success
        );
    
        var reply = await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);
        if (reply.IsSuccess && _context.TryGetUserID(out var userID) && _context.TryGetChannelID(out var channelID))
            await _rollStatsService.RecordAsync(reply.Entity.ID, channelID, userID, roll, RollLabel.Of(embed));

        return reply;
    }

    private static IEmbedField RolledWith(IEnumerable<RollBonus> bonuses) =>
        new EmbedField("Rolled with", string.Join(", ", bonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}")));

    private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();
}

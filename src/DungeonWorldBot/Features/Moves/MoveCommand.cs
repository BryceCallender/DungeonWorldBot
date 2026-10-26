using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace DungeonWorldBot.Features.Moves;

[Group("move")]
public class MoveCommand : DungeonWorldCommandBase
{
    private const string BonusDescription = "Any +forward, ongoing bonus, or penalty to add to the roll";
    private const string TargetDescription = "The enemy you're attacking, to take your damage off it on a hit";

    private readonly ICharacterService _characterService;
    private readonly CharacterMoves _characterMoves;
    private readonly IRollStatsService _rollStatsService;
    private readonly IEncounterService _encounterService;
    private readonly MoveResolver _moveResolver;

    public MoveCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        CharacterMoves characterMoves,
        IRollStatsService rollStatsService,
        IEncounterService encounterService,
        MoveResolver moveResolver)
        : base(context, feedbackService)
    {
        _characterService = characterService;
        _characterMoves = characterMoves;
        _rollStatsService = rollStatsService;
        _encounterService = encounterService;
        _moveResolver = moveResolver;
    }

    [Command("hack-and-slash")]
    [Description("Attack an enemy in melee (+STR)")]
    public Task<IResult> HackAndSlashAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(TargetDescription)] [AutocompleteProvider(TargetAutocomplete.ID)] string? target = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        => MakeStatMoveAsync(BasicMoves.HackAndSlash, StatType.Str, bonus, target, extraDamage);

    [Command("volley")]
    [Description("Take aim and shoot at an enemy at range (+DEX)")]
    public Task<IResult> VolleyAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(TargetDescription)] [AutocompleteProvider(TargetAutocomplete.ID)] string? target = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        => MakeStatMoveAsync(BasicMoves.Volley, StatType.Dex, bonus, target, extraDamage);

    [Command("defy-danger")]
    [Description("Act despite an imminent threat (+the stat that fits how you do it)")]
    public Task<IResult> DefyDangerAsync(
        [Description("STR: power through, DEX: dodge, CON: endure, INT: think fast, WIS: resist, CHA: charm")]
        StatType stat,
        [Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.DefyDanger, stat, bonus);

    [Command("defend")]
    [Description("Stand in defense of a person, item, or location (+CON)")]
    public Task<IResult> DefendAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.Defend, StatType.Con, bonus);

    [Command("spout-lore")]
    [Description("Consult your accumulated knowledge about something (+INT)")]
    public Task<IResult> SpoutLoreAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.SpoutLore, StatType.Int, bonus);

    [Command("discern-realities")]
    [Description("Closely study a situation or person (+WIS)")]
    public Task<IResult> DiscernRealitiesAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.DiscernRealities, StatType.Wis, bonus);

    [Command("parley")]
    [Description("Use leverage to manipulate a GM character (+CHA)")]
    public Task<IResult> ParleyAsync([Description(BonusDescription)] int bonus = 0)
        => MakeStatMoveAsync(BasicMoves.Parley, StatType.Cha, bonus);

    [Command("aid")]
    [Description("Help or hinder another player's character (+bonds with them)")]
    public async Task<IResult> AidOrInterfereAsync(
        [Description("The player you're helping or hindering")]
        IUser user,
        [Description(BonusDescription)] int bonus = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        var bonds = character.Bonds?.Count(b => b.TargetID == user.ID) ?? 0;
        var ally = await _characterService.GetCharacterNameAsync(user.ID) ?? user.Username;
        return await ReplyWithMoveAsync(character, BasicMoves.AidOrInterfere, $"Bond with {user.Username}", bonds, bonus, ally: ally);
    }

    [Command("last-breath")]
    [Description("You're dying. Roll to see what lies beyond the Black Gates (+nothing)")]
    public async Task<IResult> LastBreathAsync()
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        return await ReplyWithMoveAsync(character, BasicMoves.LastBreath, modifierLabel: null, modifier: 0, bonus: 0, kind: RollKind.LastBreath);
    }

    [Command("class")]
    [Description("Roll one of your class or subclass moves, like Channel or Raise the Dead")]
    public async Task<IResult> ClassMoveAsync(
        [Description("The class move to roll")]
        [AutocompleteProvider(RolledClassMoveAutocomplete.ID)]
        string move,
        [Description(BonusDescription)] int bonus = 0,
        [Description("Roll with a different stat than usual, if a move lets you")]
        StatType? stat = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription + ", if the move deals damage")]
        int extraDamage = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        var prepared = _characterMoves.PrepareClassMove(character, move, stat);
        if (prepared.Error is { } error)
            return await ReplyWithErrorAsync(error);

        var input = prepared.Input!;
        return await ReplyWithMoveAsync(character, prepared.Move!, input.ModifierLabel, input.Modifier, bonus, input.Stat, extraDamage: extraDamage);
    }

    /// <param name="target">The enemy to take the damage off, for moves that roll it.</param>
    /// <param name="extraDamage">Flat damage added to the damage roll, for something the bot doesn't track.</param>
    private async Task<IResult> MakeStatMoveAsync(MoveDefinition move, StatType statType, int bonus, string? target = null, int extraDamage = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync("You must have a character to make moves. Try using /character create");

        var stat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (stat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        return await ReplyWithMoveAsync(character, move, statType.ToString().ToUpper(), stat.Modifier, bonus, statType, target: target, extraDamage: extraDamage);
    }

    /// <param name="target">The enemy to take the damage off, if the move rolls damage and hits.</param>
    /// <param name="stat">The stat being rolled, so its debility (if any) applies.</param>
    /// <param name="kind">What the roll is for, when subclass bonuses (like a Boon of Luna) could apply to it.</param>
    /// <param name="extraDamage">Flat damage added to the damage roll, if the move rolls damage.</param>
    /// <param name="ally">The player's character being helped or hindered, for the spotlight.</param>
    private async Task<IResult> ReplyWithMoveAsync(
        Character character,
        MoveDefinition move,
        string? modifierLabel,
        int modifier,
        int bonus,
        StatType? stat = null,
        RollKind? kind = null,
        string? target = null,
        int extraDamage = 0,
        string? ally = null)
    {
        if (TacticRules.WhyCannotMake(character, move.Name) is { } forbidden)
            return await ReplyWithErrorAsync(forbidden);

        var resolution = await _moveResolver.ResolveAsync(
            character,
            move,
            new MoveInput(modifierLabel, modifier, bonus, stat, kind, target, extraDamage));

        var reply = await _feedbackService.SendContextualEmbedAsync(resolution.Embed, ct: CancellationToken);

        if (!reply.IsSuccess)
            return reply;

        if (_context.TryGetChannelID(out var channelID))
            await _rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, resolution.Roll.Dice, RollLabel.Of(resolution.Embed));

        // Does nothing between fights
        await _encounterService.LogAsync(resolution.ToAction(reply.Entity.ID, ally));

        return reply;
    }

    private async Task<Character?> GetOwnCharacterAsync() =>
        await _characterService.GetCharacterFromUserAsync(GetUserId());
}

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
public class MoveCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    CharacterMoves characterMoves,
    IRollStatsService rollStatsService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    IResourceService resourceService)
    : MoveCommandBase(context, feedbackService, characterService, rollStatsService, encounterService, moveResolver, resourceService)
{
    private const string TargetDescription = "The enemy you're attacking, to take your damage off it on a hit";

    [Command("hack-and-slash")]
    [Description("Attack an enemy in melee (+STR)")]
    public Task<IResult> HackAndSlashAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Description(TargetDescription)] [AutocompleteProvider(TargetAutocomplete.ID)] string? target = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        => MakeStatMoveAsync(BasicMoves.HackAndSlash, StatType.Str, bonus, target, extraDamage, prepared: prepared);

    [Command("volley")]
    [Description("Take aim and shoot at an enemy at range (+DEX)")]
    public Task<IResult> VolleyAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Description(TargetDescription)] [AutocompleteProvider(TargetAutocomplete.ID)] string? target = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
        => MakeStatMoveAsync(BasicMoves.Volley, StatType.Dex, bonus, target, extraDamage, prepared: prepared);

    [Command("defy-danger")]
    [Description("Act despite an imminent threat (+the stat that fits how you do it)")]
    public Task<IResult> DefyDangerAsync(
        [Description("STR: power through, DEX: dodge, CON: endure, INT: think fast, WIS: resist, CHA: charm")]
        StatType stat,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(BasicMoves.DefyDanger, stat, bonus, prepared: prepared);

    [Command("defend")]
    [Description("Stand in defense of a person, item, or location (+CON)")]
    public Task<IResult> DefendAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(BasicMoves.Defend, StatType.Con, bonus, prepared: prepared);

    [Command("spout-lore")]
    [Description("Consult your accumulated knowledge about something (+INT)")]
    public Task<IResult> SpoutLoreAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(BasicMoves.SpoutLore, StatType.Int, bonus, prepared: prepared);

    [Command("discern-realities")]
    [Description("Closely study a situation or person (+WIS)")]
    public Task<IResult> DiscernRealitiesAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(BasicMoves.DiscernRealities, StatType.Wis, bonus, prepared: prepared);

    [Command("parley")]
    [Description("Use leverage to manipulate a GM character (+CHA)")]
    public Task<IResult> ParleyAsync(
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
        => MakeStatMoveAsync(BasicMoves.Parley, StatType.Cha, bonus, prepared: prepared);

    [Command("aid")]
    [Description("Help or hinder another player's character (+bonds with them)")]
    public async Task<IResult> AidOrInterfereAsync(
        [Description("The player you're helping or hindering")]
        IUser user,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        var bonds = character.Bonds?.Count(b => b.TargetID == user.ID) ?? 0;
        var ally = await _characterService.GetCharacterNameAsync(user.ID) ?? user.Username;
        return await ReplyWithMoveAsync(character, BasicMoves.AidOrInterfere, $"Bond with {user.Username}", bonds, bonus, ally: ally, prepared: prepared);
    }

    [Command("last-breath")]
    [Description("You're dying. Roll to see what lies beyond the Black Gates (+nothing)")]
    public async Task<IResult> LastBreathAsync([Description(PreparedDescription)] bool prepared = false)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        return await ReplyWithMoveAsync(character, BasicMoves.LastBreath, modifierLabel: null, modifier: 0, bonus: 0, kind: RollKind.LastBreath, prepared: prepared);
    }

    [Command("class")]
    [Description("Roll one of your class or subclass moves, like Channel or Raise the Dead")]
    public async Task<IResult> ClassMoveAsync(
        [Description("The class move to roll")]
        [AutocompleteProvider(RolledClassMoveAutocomplete.ID)]
        string move,
        [Description(BonusDescription)] int bonus = 0,
        [Description(PreparedDescription)] bool prepared = false,
        [Description("Roll with a different stat than usual, if a move lets you")]
        StatType? stat = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription + ", if the move deals damage")]
        int extraDamage = 0)
    {
        var character = await GetOwnCharacterAsync();
        if (character is null)
            return await ReplyWithErrorAsync(NoMoverMessage);

        var classMove = characterMoves.PrepareClassMove(character, move, stat);
        if (classMove.Error is { } error)
            return await ReplyWithErrorAsync(error);

        var input = classMove.Input!;
        return await ReplyWithMoveAsync(character, classMove.Move!, input.ModifierLabel, input.Modifier, bonus, input.Stat, extraDamage: extraDamage, prepared: prepared);
    }
}

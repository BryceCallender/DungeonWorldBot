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
    private readonly IDiceRoller _diceRoller;
    private readonly IResourceService _resourceService;
    private readonly IRollStatsService _rollStatsService;
    private readonly IDamageService _damageService;
    private readonly IEncounterService _encounterService;

    public MoveCommand(
        ICommandContext context,
        FeedbackService feedbackService,
        ICharacterService characterService,
        IDiceRoller diceRoller,
        IResourceService resourceService,
        IRollStatsService rollStatsService,
        IDamageService damageService,
        IEncounterService encounterService)
        : base(context, feedbackService)
    {
        _characterService = characterService;
        _diceRoller = diceRoller;
        _resourceService = resourceService;
        _rollStatsService = rollStatsService;
        _damageService = damageService;
        _encounterService = encounterService;
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

        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return await ReplyWithErrorAsync($"The {character.Class.Type} doesn't have a class sheet in the bot yet.");

        var classMove = sheet.FindMove(move);
        var rollsFrom = sheet;
        if (classMove is null
            && Multiclassing.KnownForeignMoves(character, sheet).FirstOrDefault(f => f.Move.Name.Equals(move.Trim(), StringComparison.OrdinalIgnoreCase)) is { } foreign)
        {
            // A move learned from another class's sheet
            classMove = foreign.Move;
            rollsFrom = foreign.Sheet;
        }

        classMove ??= SubclassLibrary.Of(character)?.FindMove(move);

        if (classMove?.Roll is not { } definition)
            return await ReplyWithErrorAsync($"{character.Name} doesn't have a rolled class move called {move}.");

        if (!ClassSheetLibrary.KnownMoves(character).Contains(classMove.Name, StringComparer.OrdinalIgnoreCase))
            return await ReplyWithErrorAsync($"{character.Name} doesn't know {classMove.Name}. Learn it with /class learn");

        if (stat is null && classMove.RollsWith is { } rollsWith)
        {
            if (rollsFrom.FindResource(rollsWith) is { } resource)
            {
                var value = _resourceService.GetValue(character, resource);
                return await ReplyWithMoveAsync(character, definition, resource.Name, value, bonus, extraDamage: extraDamage);
            }

            if (rollsFrom.ClassStat?.Name.Equals(rollsWith, StringComparison.OrdinalIgnoreCase) == true)
            {
                stat = rollsFrom == sheet ? ClassSheetLibrary.ResolveClassStat(character, sheet) : null;
                if (stat is null)
                {
                    return await ReplyWithErrorAsync(rollsFrom == sheet
                        ? $"Choose your {sheet.ClassStat.Feature} with /class learn first, it decides your {rollsWith} stat."
                        : $"{classMove.Name} rolls the {rollsFrom.Type}'s {rollsWith} stat. Pick which stat to use with the stat option.");
                }
            }
        }

        if ((stat ?? definition.Stat) is not { } statType)
            return await ReplyWithMoveAsync(character, definition, modifierLabel: null, modifier: 0, bonus, extraDamage: extraDamage);

        var characterStat = character.Stats.FirstOrDefault(s => s.StatType == statType);
        if (characterStat is null)
            return await ReplyWithErrorAsync($"Your character has no {statType} stat.");

        return await ReplyWithMoveAsync(character, definition, statType.ToString().ToUpper(), characterStat.Modifier, bonus, statType, extraDamage: extraDamage);
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

        var debility = stat is { } rolled ? DebilityRules.RollPenalty(character, rolled) : null;
        kind ??= stat is not null ? RollKind.Stat : null;
        var subclassBonuses = kind is { } rollKind ? await _damageService.BonusesAsync(character, rollKind, stat) : [];
        if (TacticRules.RollBonus(character, move.Name) is not 0 and var tacticBonus)
            subclassBonuses = subclassBonuses.Append(new RollBonus($"{TacticRules.Current(character)!.Name} Tactics", tacticBonus)).ToList();
        var roll = MoveRoller.Roll(_diceRoller, modifier + bonus + (debility?.Penalty ?? 0) + subclassBonuses.Sum(b => b.Amount));
        var outcome = TacticRules.Outcome(character, move.Name, roll.Outcome);

        var fields = new List<IEmbedField>();

        if (modifierLabel is not null || bonus != 0 || debility is not null || subclassBonuses.Count > 0)
        {
            var parts = new List<string>();
            if (modifierLabel is not null)
                parts.Add($"{modifierLabel} {FormatSigned(modifier)}");
            if (debility is { } penalty)
                parts.Add($"{penalty.Debility} {FormatSigned(penalty.Penalty)}");
            parts.AddRange(subclassBonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}"));
            if (bonus != 0)
                parts.Add($"Bonus {FormatSigned(bonus)}");

            fields.Add(new EmbedField("Rolled with", string.Join(", ", parts), IsInline: true));
        }

        if (outcome != roll.Outcome)
            fields.Add(new EmbedField($"{TacticRules.Current(character)!.Name} Tactics", $"Your {MoveRoller.Describe(roll.Outcome)} counts as a {MoveRoller.Describe(outcome)}."));

        fields.Add(new EmbedField(MoveRoller.Describe(outcome), move.Describe(outcome)));

        var isHit = outcome != MoveOutcome.Miss;
        if (move.ShowsOptions(outcome))
        {
            fields.Add(new EmbedField("Options", string.Join('\n', move.Options!.Select(o => $"• {o}"))));
        }

        DamageDealt? dealt = null;
        if (move.RollsDamage && (isHit || TacticRules.DamagesOnMiss(character, move.Name)))
        {
            if (!isHit)
                fields.Add(new EmbedField($"{TacticRules.Current(character)!.Name} Tactics", "Your damage lands even on a miss."));
            dealt = await RollDamageAsync(character, move.Name, target, extraDamage);
            fields.AddRange(dealt.Fields);
        }

        if (isHit && move.Name == TacticRules.ChangeTactics)
        {
            fields.Add(new EmbedField("New Tactic", $"Now in {TacticRules.Current(character)?.Name ?? "no"} Tactics. Switch with /class tactic change"));
        }

        var colour = MoveRoller.ColourFor(outcome);

        var embed = new Embed(
            Title: $"🎲 {character.Name}: {move.Name}",
            Description: $"*{move.Trigger}...*\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
            Fields: fields,
            Colour: colour
        );

        var reply = await _feedbackService.SendContextualEmbedAsync(embed, ct: CancellationToken);

        if (!reply.IsSuccess)
            return reply;

        if (_context.TryGetChannelID(out var channelID))
            await _rollStatsService.RecordAsync(reply.Entity.ID, channelID, character.ID, roll.Dice, RollLabel.Of(embed));

        // Does nothing between fights
        await _encounterService.LogAsync(new EncounterAction
        {
            Actor = character.Name,
            CharacterID = character.ID,
            MessageID = reply.Entity.ID,
            Action = move.Name,
            Roll = $"{roll.Dice.Value} · {MoveRoller.Describe(outcome)}",
            Target = dealt?.Target ?? ally,
            Result = dealt?.Result,
        });

        return reply;
    }

    /// <summary>
    /// A move's damage, and what it did to the enemy it was aimed at.
    /// </summary>
    /// <param name="Target">The enemy it hit, if there was one in the fight.</param>
    /// <param name="Result">What the damage came to, e.g. "took 4" for an enemy or "6 damage" with none.</param>
    private record DamageDealt(IReadOnlyList<IEmbedField> Fields, string? Target = null, string? Result = null);

    private async Task<DamageDealt> RollDamageAsync(Character character, string move, string? target, int extra)
    {
        if (await _damageService.RollAsync(character, move, extra) is not { } damage)
            return new DamageDealt([new EmbedField("Damage", $"Couldn't roll `{character.DamageModifier ?? character.Class.Damage}`. Fix it with /character damage-dice")]);

        var fields = new List<IEmbedField> { new EmbedField("Damage", $"{damage.Expression} → **{damage.Total}**{damage.Details}") };
        if (string.IsNullOrWhiteSpace(target))
            return new DamageDealt(fields, Result: $"{damage.Total} damage");

        var hit = await EnemyHit.ApplyAsync(_encounterService, target, damage.Total);
        fields.Add(hit.Field);
        return new DamageDealt(fields, hit.Target, hit.Result ?? $"{damage.Total} damage");
    }

    private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();

    private async Task<Character?> GetOwnCharacterAsync() =>
        await _characterService.GetCharacterFromUserAsync(GetUserId());
}

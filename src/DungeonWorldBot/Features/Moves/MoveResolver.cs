using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// What a character's move rolls with, besides the move itself.
/// </summary>
/// <param name="ModifierLabel">What the modifier is, e.g. "STR" or "Bond with Aria", or null when there's none.</param>
/// <param name="Stat">The stat being rolled, so its debility (if any) applies.</param>
/// <param name="Kind">What the roll is for, when subclass bonuses (like a Boon of Luna) could apply to it.</param>
/// <param name="Target">The enemy to take the damage off, if the move rolls damage and hits.</param>
/// <param name="ExtraDamage">Flat damage added to the damage roll, if the move rolls damage.</param>
public record MoveInput(
    string? ModifierLabel,
    int Modifier,
    int Bonus,
    StatType? Stat = null,
    RollKind? Kind = null,
    string? Target = null,
    int ExtraDamage = 0)
{
    /// <summary>
    /// A move rolled with one of the character's stats, or null if they don't have it.
    /// </summary>
    public static MoveInput? ForStat(Character character, StatType stat, int bonus, string? target = null, int extraDamage = 0) =>
        character.Stats.FirstOrDefault(s => s.StatType == stat) is { } rolled
            ? new MoveInput(stat.ToString().ToUpper(), rolled.Modifier, bonus, stat, Target: target, ExtraDamage: extraDamage)
            : null;
}

/// <summary>
/// A move a character made: what it rolled, the embed telling the table, and what its damage did.
/// </summary>
/// <param name="Outcome">The result, after anything (like Tactics) that changes it.</param>
/// <param name="Dealt">The move's damage, if it rolled any.</param>
public record MoveResolution(Character Character, MoveDefinition Move, Embed Embed, MoveRoll Roll, MoveOutcome Outcome, DamageDealt? Dealt)
{
    /// <summary>
    /// The move, for the spotlight.
    /// </summary>
    /// <param name="messageID">The message the embed went out in.</param>
    /// <param name="ally">The player's character being helped or hindered, if anyone.</param>
    public EncounterAction ToAction(Snowflake? messageID, string? ally = null) => new()
    {
        Actor = Character.Name,
        CharacterID = Character.ID,
        MessageID = messageID,
        Action = Move.Name,
        Roll = $"{Roll.Dice.Value} · {MoveRoller.Describe(Outcome)}",
        Target = Dealt?.Target ?? ally,
        Result = Dealt?.Result,
    };
}

/// <summary>
/// Rolls a character's move with everything that changes it: debilities, subclass bonuses, Tactics, and damage on a hit.
/// Used by /move and by moves made from a scene on the dashboard, so both play by the same rules.
/// </summary>
/// <remarks>
/// Check <see cref="TacticRules.WhyCannotMake"/> first: this rolls whatever it's given.
/// </remarks>
[Scoped]
public class MoveResolver
{
    private readonly IDiceRoller _diceRoller;
    private readonly IDamageService _damageService;
    private readonly IEncounterService _encounterService;

    public MoveResolver(IDiceRoller diceRoller, IDamageService damageService, IEncounterService encounterService)
    {
        _diceRoller = diceRoller;
        _damageService = damageService;
        _encounterService = encounterService;
    }

    public async Task<MoveResolution> ResolveAsync(Character character, MoveDefinition move, MoveInput input)
    {
        var debility = input.Stat is { } rolled ? DebilityRules.RollPenalty(character, rolled) : null;
        var kind = input.Kind ?? (input.Stat is not null ? RollKind.Stat : null);
        var subclassBonuses = kind is { } rollKind ? await _damageService.BonusesAsync(character, rollKind, input.Stat) : [];
        if (TacticRules.RollBonus(character, move.Name) is not 0 and var tacticBonus)
            subclassBonuses = subclassBonuses.Append(new RollBonus($"{TacticRules.Current(character)!.Name} Tactics", tacticBonus)).ToList();
        var roll = MoveRoller.Roll(_diceRoller, input.Modifier + input.Bonus + (debility?.Penalty ?? 0) + subclassBonuses.Sum(b => b.Amount));
        var outcome = TacticRules.Outcome(character, move.Name, roll.Outcome);

        var fields = new List<IEmbedField>();

        if (input.ModifierLabel is not null || input.Bonus != 0 || debility is not null || subclassBonuses.Count > 0)
        {
            var parts = new List<string>();
            if (input.ModifierLabel is not null)
                parts.Add($"{input.ModifierLabel} {FormatSigned(input.Modifier)}");
            if (debility is { } penalty)
                parts.Add($"{penalty.Debility} {FormatSigned(penalty.Penalty)}");
            parts.AddRange(subclassBonuses.Select(b => $"{b.Source} {FormatSigned(b.Amount)}"));
            if (input.Bonus != 0)
                parts.Add($"Bonus {FormatSigned(input.Bonus)}");

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
            dealt = await RollDamageAsync(character, move.Name, input.Target, input.ExtraDamage);
            fields.AddRange(dealt.Fields);
        }

        if (isHit && move.Name == TacticRules.ChangeTactics)
        {
            fields.Add(new EmbedField("New Tactic", $"Now in {TacticRules.Current(character)?.Name ?? "no"} Tactics. Switch with /class tactic change"));
        }

        var embed = new Embed(
            Title: $"🎲 {character.Name}: {move.Name}",
            Description: $"*{move.Trigger}...*\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
            Fields: fields,
            Colour: MoveRoller.ColourFor(outcome)
        );

        return new MoveResolution(character, move, embed, roll, outcome, dealt);
    }

    private async Task<DamageDealt> RollDamageAsync(Character character, string move, string? target, int extra)
    {
        if (await _damageService.RollAsync(character, move, extra) is not { } damage)
            return new DamageDealt([new EmbedField("Damage", $"Couldn't roll `{character.DamageModifier ?? character.Class.Damage}`. Fix it with /character damage-dice")]);

        var fields = new List<IEmbedField> { new EmbedField("Damage", $"{damage.Expression} → **{damage.Total}**{damage.Details}") };
        if (string.IsNullOrWhiteSpace(target))
            return new DamageDealt(fields, Result: $"💥 {damage.Total} damage");

        var hit = await EnemyHit.ApplyAsync(_encounterService, target, damage.Total);
        fields.Add(hit.Field);
        return new DamageDealt(fields, hit.Target, hit.Result ?? $"💥 {damage.Total} damage", hit.Taken);
    }

    private static string FormatSigned(int value) => value >= 0 ? $"+{value}" : value.ToString();
}

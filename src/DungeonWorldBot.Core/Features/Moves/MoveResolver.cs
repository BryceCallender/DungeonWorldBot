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
/// <param name="Prepared">Whether they spent a Preparation from Bolster for +1. Spend it before resolving.</param>
/// <param name="Damage">The move's own damage, rolled on a hit in place of the character's.</param>
/// <param name="Appetite">The appetite a Barbarian is pursuing, if any, rolling 1d6+1d8 in place of 2d6.</param>
/// <param name="Piercing">Armor the character's damage ignores, like Arrows of Light.</param>
public record MoveInput(
    string? ModifierLabel,
    int Modifier,
    int Bonus,
    StatType? Stat = null,
    RollKind? Kind = null,
    string? Target = null,
    int ExtraDamage = 0,
    bool Prepared = false,
    MoveDamage? Damage = null,
    string? Appetite = null,
    int Piercing = 0)
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
/// Damage a move deals with its own dice rather than the character's, like a Channeler's Technique. Gear and subclass
/// damage bonuses don't add to it.
/// </summary>
/// <param name="Dice">e.g. "1d8".</param>
/// <param name="Source">Where the damage comes from, e.g. "Blast".</param>
/// <param name="Piercing">Armor it ignores.</param>
/// <param name="RiskDice">d4s added to it that also hurt the character (ignoring armor) when they show 1 or 2, like
/// Thread the Needle.</param>
public record MoveDamage(string Dice, string Source, int Piercing = 0, int RiskDice = 0);

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
public class MoveResolver(IDiceRoller diceRoller, IDamageService damageService, IEncounterService encounterService)
{
    private const string RiskDie = "d4";
    private const int RiskBacklash = 2;

    public async Task<MoveResolution> ResolveAsync(Character character, MoveDefinition move, MoveInput input)
    {
        var debility = input.Stat is { } rolled ? DebilityRules.RollPenalty(character, rolled) : null;
        var kind = input.Kind ?? (input.Stat is not null ? RollKind.Stat : null);
        var subclassBonuses = kind is { } rollKind ? await damageService.BonusesAsync(character, rollKind, input.Stat) : [];
        if (TacticRules.RollBonus(character, move.Name) is not 0 and var tacticBonus)
            subclassBonuses = subclassBonuses.Append(new RollBonus($"{TacticRules.Current(character)!.Name} Tactics", tacticBonus)).ToList();
        if (input.Prepared)
            subclassBonuses = subclassBonuses.Append(new RollBonus(SpecialMoveRules.Preparation.Name, 1)).ToList();
        var roll = MoveRoller.Roll(diceRoller, input.Modifier + input.Bonus + (debility?.Penalty ?? 0) + subclassBonuses.Sum(b => b.Amount), input.Appetite is not null);
        var outcome = TacticRules.Outcome(character, move.Name, roll.Outcome);

        List<IEmbedField> fields = [];

        if (input.ModifierLabel is not null || input.Bonus != 0 || debility is not null || subclassBonuses.Count > 0)
        {
            List<string> parts = [];
            if (input.ModifierLabel is not null)
                parts.Add($"{input.ModifierLabel} {Numbers.Signed(input.Modifier)}");
            if (debility is { } penalty)
                parts.Add($"{penalty.Debility} {Numbers.Signed(penalty.Penalty)}");
            parts.AddRange(subclassBonuses.Select(b => $"{b.Source} {Numbers.Signed(b.Amount)}"));
            if (input.Bonus != 0)
                parts.Add($"Bonus {Numbers.Signed(input.Bonus)}");

            fields.Add(new EmbedField("Rolled with", string.Join(", ", parts), IsInline: true));
        }

        if (outcome != roll.Outcome)
            fields.Add(new EmbedField($"{TacticRules.Current(character)!.Name} Tactics", $"Your {MoveRoller.Describe(roll.Outcome)} counts as a {MoveRoller.Describe(outcome)}."));

        fields.Add(new EmbedField(MoveRoller.Describe(outcome), move.Describe(outcome)));

        if (input.Appetite is { } appetite)
        {
            fields.Add(new EmbedField($"🍖 Pursuing {appetite}", MoveRoller.AppetiteBites(roll.Dice)
                ? $"Rolled {MoveRoller.AppetiteDice}. The d6 is higher: the GM adds a complication or danger from your heedless pursuit."
                : $"Rolled {MoveRoller.AppetiteDice}."));
        }

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
            dealt = input.Damage is { } own
                ? await RollOwnDamageAsync(character, own, input.Target, input.ExtraDamage)
                : await RollDamageAsync(character, move.Name, input.Target, input.ExtraDamage, input.Piercing);
            fields.AddRange(dealt.Fields);
        }

        if (isHit && move.Name == TacticRules.ChangeTactics)
        {
            fields.Add(new EmbedField("New Tactic", $"Now in {TacticRules.Current(character)?.Name ?? "no"} Tactics. Switch with /tactic change"));
        }

        var embed = new Embed(
            Title: $"🎲 {character.Name}: {move.Name}",
            Description: $"*{move.Trigger}...*\n\n{DiceHelper.BuildDiceResult(roll.Dice)}",
            Fields: fields,
            Colour: MoveRoller.ColorFor(outcome)
        );

        return new MoveResolution(character, move, embed, roll, outcome, dealt);
    }

    private async Task<DamageDealt> RollDamageAsync(Character character, string move, string? target, int extra, int piercing)
    {
        if (await damageService.RollAsync(character, move, extra) is not { } damage)
            return new DamageDealt([new EmbedField("Damage", $"Couldn't roll `{character.DamageModifier ?? character.Class.Damage}`. Fix it with /character damage-dice")]);

        var fields = new List<IEmbedField> { new EmbedField("Damage", $"{damage.Expression} → **{damage.Total}**{damage.Details}") };
        if (string.IsNullOrWhiteSpace(target))
            return new DamageDealt(fields, Result: $"💥 {damage.Total} damage");

        return await HitTargetAsync(fields, target, damage.Total, piercing);
    }

    private async Task<DamageDealt> RollOwnDamageAsync(Character character, MoveDamage damage, string? target, int extra)
    {
        var expression = DiceHelper.AddModifier(damage.Dice, extra);
        if (!diceRoller.TryRoll(expression, out var dice))
            return new DamageDealt([new EmbedField("Damage", $"Couldn't roll `{expression}`.")]);

        var risk = Enumerable.Range(0, damage.RiskDice).Select(_ => diceRoller.Roll($"1{RiskDie}").Value).ToList();
        var total = Math.Max(0, dice.Value) + risk.Sum();

        var rolled = $"{damage.Source} {expression} → {dice.Value}";
        if (risk.Count > 0)
            rolled += $" + {risk.Count}{RiskDie} → {string.Join(", ", risk)}";
        if (damage.Piercing > 0)
            rolled += $" · {damage.Piercing} piercing";

        var fields = new List<IEmbedField> { new EmbedField("Damage", $"{rolled} = **{total}**") };

        // Each risk die showing 1 or 2 hurts the character by what it shows
        var backlash = risk.Where(r => r <= RiskBacklash).Sum();
        if (backlash > 0)
        {
            var hurt = await damageService.TakeAsync(character, backlash, DamageType.True);
            fields.Add(new EmbedField("Backlash", hurt.Describe(character)));
        }

        if (string.IsNullOrWhiteSpace(target))
            return new DamageDealt(fields, Result: $"💥 {total} damage");

        return await HitTargetAsync(fields, target, total, damage.Piercing);
    }

    private async Task<DamageDealt> HitTargetAsync(List<IEmbedField> fields, string target, int total, int piercing = 0)
    {
        var hit = await EnemyHit.ApplyAsync(encounterService, target, total, piercing);
        fields.Add(hit.Field);
        return new DamageDealt(fields, hit.Target, hit.Result ?? $"💥 {total} damage", hit.Taken);
    }
}

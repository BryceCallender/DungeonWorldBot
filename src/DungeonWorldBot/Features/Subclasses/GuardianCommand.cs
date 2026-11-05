using System.ComponentModel;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Abilities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Features.Subclasses.Homebrew;
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

namespace DungeonWorldBot.Features.Subclasses;

/// <summary>
/// The Tactical Guardian's moves that need more than a plain roll: the strategy pool, stored damage, and Perfected
/// Assault's short-rest limit. Flashstep Exchange and Guardian's Stand are rolled with /move class.
/// </summary>
[Group("guardian")]
public class GuardianCommand(
    ICommandContext context,
    FeedbackService feedbackService,
    ICharacterService characterService,
    IAbilityService abilityService,
    ISubclassService subclassService,
    IDiceRoller diceRoller,
    StatRoller statRoller,
    IRollStatsService rollStatsService,
    IDamageService damageService,
    IArmorService armorService)
    : RollingCommandBase(context, feedbackService, characterService, statRoller, rollStatsService)
{

    [Command("read-the-room")]
    [Description("A fight is about to break out: roll +INT and gain a strategy pool of d6s equal to your roll")]
    public async Task<IResult> ReadTheRoomAsync([Description(BonusDescription)] int bonus = 0)
    {
        var found = await GetGuardianAsync(TacticalGuardian.ReadTheRoom);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        var definition = TacticalGuardian.ReadTheRoomRoll;
        var (roll, rolledWith) = await RollStatAsync(character, StatType.Int, bonus);

        var size = TacticalGuardian.PoolSize(roll.Dice.Value);
        var pool = Enumerable.Range(0, size).Select(_ => diceRoller.Roll("1d6").Value).ToList();
        await SetPoolAsync(character, pool);

        List<IEmbedField> fields =
        [
            new EmbedField("Rolled with", rolledWith, IsInline: true),
            new EmbedField(MoveRoller.Describe(roll.Outcome), definition.Describe(roll.Outcome)),
            new EmbedField($"Strategy pool ({pool.Count}d6)", TacticalGuardian.FormatPool(pool) +
                (size == TacticalGuardian.MaxPool && roll.Dice.Value > size ? $"\nCapped at {TacticalGuardian.MaxPool} dice." : string.Empty) +
                "\nSpend dice with /guardian one-step-ahead"),
        ];

        return await ReplyWithRollAsync(character, $"🔆 {character.Name}: {TacticalGuardian.ReadTheRoom}",
            "*The light sharpens every detail of the room...*", roll, fields);
    }

    [Command("one-step-ahead")]
    [Description("When someone else acts in combat, swap one die of their roll for one from your strategy pool")]
    public async Task<IResult> OneStepAheadAsync(
        [Description("The die from your strategy pool to use")]
        [MinValue(1)]
        [MaxValue(6)]
        int die,
        [Description("Whose roll you're changing, and how you influence it")]
        string? how = null)
    {
        var found = await GetGuardianAsync(TacticalGuardian.OneStepAhead);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        var pool = TacticalGuardian.PoolOf(character);
        if (pool.Count == 0)
            return await ReplyWithErrorAsync($"{character.Name}'s strategy pool is empty. Fill it with /guardian read-the-room");

        if (TacticalGuardian.Spend(pool, die) is not { } left)
            return await ReplyWithErrorAsync($"There's no {die} in {character.Name}'s strategy pool: {TacticalGuardian.FormatPool(pool)}");

        await SetPoolAsync(character, left);

        var description = $"*{character.Name} moves before anyone else can...*\n\nReplace one die of the roll with a **{die}**.";
        if (!string.IsNullOrWhiteSpace(how))
            description += $"\n\n{how.Trim()}";

        return await ReplyAsync($"⚡ {character.Name}: {TacticalGuardian.OneStepAhead}", description,
            [new EmbedField("Strategy pool", TacticalGuardian.FormatPool(left))]);
    }

    [Command("pool")]
    [Description("See your strategy pool and stored damage")]
    public async Task<IResult> PoolAsync()
    {
        var found = await GetGuardianAsync(TacticalGuardian.ReadTheRoom);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        List<IEmbedField> fields =
        [
            new EmbedField("Strategy pool", TacticalGuardian.FormatPool(TacticalGuardian.PoolOf(character))),
        ];
        if (abilityService.FindAbility(character, TacticalGuardian.ShieldedPursuit) is not null)
            fields.Add(new EmbedField("Stored damage", TacticalGuardian.StoredDamageOf(character).ToString()));

        return await ReplyAsync($"{character.Name}'s Strategy", "*Every die a plan, every plan a step ahead.*", fields);
    }

    [Command("take-hit")]
    [Description("Take damage while defending. With Shielded Pursuit, half of it is stored")]
    public async Task<IResult> TakeHitAsync(
        [Description("The damage dealt, before your armor")]
        [MinValue(0)]
        int damage,
        [Option("for-ally")]
        [Description("You're taking an ally's hit with Guardian's Stand, which halves it (rounded down) before your armor")]
        bool forAlly = false,
        [Description("Physical, Magical or Divine (divine weapons), each stopped by its own armor, or True (ignores armor)")]
        DamageType type = DamageType.Physical,
        [Description("How much of your armor the hit ignores")]
        [MinValue(0)]
        int piercing = 0)
    {
        var found = await GetGuardianAsync(forAlly ? TacticalGuardian.GuardiansStand : TacticalGuardian.ReadTheRoom);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        var hit = await armorService.TakeDamageAsync(character, TacticalGuardian.DamageTaken(damage, forAlly), type, piercing);
        var taken = hit.Taken;

        List<IEmbedField> fields =
        [
            new EmbedField("Damage taken", (forAlly ? $"{damage} halved → {hit.Damage}\n" : string.Empty) + DamageRules.Describe(hit) +
                (hit.Broken.Count > 0 ? $"\n*Broken: {string.Join(", ", hit.Broken.Select(b => b.Source))}*" : string.Empty) +
                $"\nHP: {character.Health.CurrentHP}/{character.Health.MaxHP}" +
                (character.Health.CurrentHP == 0 ? " · at 0 HP, roll /move last-breath" : string.Empty)),
        ];

        if (abilityService.FindAbility(character, TacticalGuardian.ShieldedPursuit) is not null)
        {
            var stored = TacticalGuardian.Stores(taken);
            var total = TacticalGuardian.StoredDamageOf(character) + stored;
            await SetStoredDamageAsync(character, total);
            fields.Add(new EmbedField(TacticalGuardian.ShieldedPursuit, $"+{stored} stored → **{total}** ready to release with /guardian release"));
        }

        return await ReplyAsync($"🛡️ {character.Name} takes the hit",
            forAlly ? "*Standing in the breach, they take the blow meant for an ally...*" : "*They hold the line...*", fields);
    }

    [Command("release")]
    [Description("Shielded Pursuit: add your stored damage to an attack")]
    public async Task<IResult> ReleaseAsync(
        [Description("The damage your attack already rolled. Leave blank to roll your damage now.")]
        [MinValue(0)]
        int? damage = null,
        [Option(DamageService.ExtraOption)] [Description(DamageService.ExtraDescription)] int extraDamage = 0)
    {
        var found = await GetGuardianAsync(TacticalGuardian.ShieldedPursuit);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        var stored = TacticalGuardian.StoredDamageOf(character);
        if (stored == 0)
            return await ReplyWithErrorAsync($"{character.Name} has no stored damage. Store some with /guardian take-hit");

        string damageText;
        if (damage is { } dealt)
        {
            var total = Math.Max(0, dealt + extraDamage) + stored;
            damageText = extraDamage != 0
                ? $"{dealt} {Numbers.Signed(extraDamage)} extra + {stored} stored → **{total}**"
                : $"{dealt} + {stored} stored → **{total}**";
        }
        else
        {
            if (await damageService.RollAsync(character, TacticalGuardian.ShieldedPursuit, extraDamage) is not { } rolled)
                return await ReplyWithErrorAsync("Couldn't roll your damage. Fix it with /character damage-dice");

            damageText = $"{rolled.Expression} → {rolled.Total} + {stored} stored → **{rolled.Total + stored}**" + rolled.Details;
        }

        await SetStoredDamageAsync(character, 0);

        return await ReplyAsync($"🗡️ {character.Name}: {TacticalGuardian.ShieldedPursuit}",
            "*The force they absorbed comes back as a spear...*", [new EmbedField("Damage", damageText)]);
    }

    [Command("end-fight")]
    [Description("The battle is over: clear your strategy pool and stored damage")]
    public async Task<IResult> EndFightAsync()
    {
        var found = await GetGuardianAsync(TacticalGuardian.ReadTheRoom);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        var pool = TacticalGuardian.PoolOf(character);
        var stored = TacticalGuardian.StoredDamageOf(character);
        await SetPoolAsync(character, []);
        await SetStoredDamageAsync(character, 0);

        List<string> cleared = [];
        if (pool.Count > 0)
            cleared.Add($"{pool.Count} unspent strategy di{(pool.Count == 1 ? "e" : "ce")}");
        if (stored > 0)
            cleared.Add($"{stored} stored damage");

        return await ReplyAsync($"🌅 {character.Name} stands down",
            cleared.Count > 0 ? $"Cleared {string.Join(" and ", cleared)}." : "Nothing left over to clear.");
    }

    [Command("perfected-assault")]
    [Description("Orchestrate a coordinated strike with every ally near you (+INT, once per short rest)")]
    public async Task<IResult> PerfectedAssaultAsync([Description(BonusDescription)] int bonus = 0)
    {
        var found = await GetGuardianAsync(TacticalGuardian.PerfectedAssault);
        if (!found.IsDefined(out var character))
            return await ReplyWithErrorAsync(found.Error!.Message);

        var ability = abilityService.FindAbility(character, TacticalGuardian.PerfectedAssault)!;
        if (ability.IsLimited && !await abilityService.UseAbilityAsync(ability, 1))
            return await ReplyWithErrorAsync($"{character.Name} has already used {TacticalGuardian.PerfectedAssault}. After a short rest, restore it with /ability restore");

        var definition = TacticalGuardian.PerfectedAssaultRoll;
        var (roll, rolledWith) = await RollStatAsync(character, StatType.Int, bonus);

        List<IEmbedField> fields =
        [
            new EmbedField("Rolled with", rolledWith, IsInline: true),
            new EmbedField(MoveRoller.Describe(roll.Outcome), definition.Describe(roll.Outcome)),
            new EmbedField("Uses", $"{ability.ToDisplay()}. Restore it after a short rest with /ability restore"),
        ];

        return await ReplyWithRollAsync(character, $"✨ {character.Name}: {TacticalGuardian.PerfectedAssault}",
            "*Every movement and attack becomes part of an orchestrated masterpiece...*", roll, fields);
    }

    private async Task SetPoolAsync(Character character, IReadOnlyList<int> pool) =>
        await SetStateAsync(character, TacticalGuardian.StrategyPool, pool.Count > 0 ? TacticalGuardian.StorePool(pool) : null);

    private async Task SetStoredDamageAsync(Character character, int stored) =>
        await SetStateAsync(character, TacticalGuardian.StoredDamage, stored > 0 ? stored.ToString() : null);

    /// <summary>
    /// Writes a state ability's description, or removes the ability when there's nothing to keep.
    /// </summary>
    private async Task SetStateAsync(Character character, string name, string? value)
    {
        var ability = abilityService.FindAbility(character, name);
        if (value is null)
        {
            if (ability is not null)
                await abilityService.RemoveAbilityAsync(character, ability);
        }
        else if (ability is null)
        {
            await abilityService.AddAbilityAsync(character, name, value, null);
        }
        else
        {
            await abilityService.SetDescriptionAsync(ability, value);
        }
    }
    /// <returns>The user's character if they're a Tactical Guardian who knows the move, or why not.</returns>
    private async Task<Result<Character>> GetGuardianAsync(string move)
    {
        var own = await RequireOwnCharacterAsync();
        if (!own.IsDefined(out var character))
            return own;

        return subclassService.WhyCantUse(character, TacticalGuardian.Subclass, move) is { } why ? new InvalidOperationError(why) : character;
    }
}

using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Combat;
using DungeonWorldBot.Features.Npcs;
using DungeonWorldBot.Features.Rolls;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// An enemy attacking a player's character or an NPC. One of the two is set.
/// </summary>
/// <param name="Move">What it does, e.g. Bite. A plain attack if not given.</param>
/// <param name="Damage">Dice to roll instead of the enemy's own damage.</param>
/// <param name="Piercing">How much of their armor the hit ignores.</param>
public record EnemyAttackInput(
    Enemy Attacker,
    Character? Character,
    Npc? Npc,
    string? Move = null,
    string? Damage = null,
    DamageType Type = DamageType.Physical,
    int Piercing = 0);

/// <summary>
/// An enemy's attack that landed: what to tell the table, and the action for the spotlight.
/// </summary>
/// <param name="Taken">The damage that got through their armor.</param>
public record EnemyAttack(string Title, string Description, string Target, int Taken, EncounterAction Action);

/// <param name="Error">Why it couldn't attack, or null when it did.</param>
public record EnemyAttackResult(string? Error, EnemyAttack? Attack)
{
    public static EnemyAttackResult Failed(string error) => new(error, null);
}

/// <summary>
/// Rolls an enemy's damage against whoever it attacks, through their armor. Used by /dm encounter attack and by attacks
/// made from a scene on the dashboard.
/// </summary>
[Scoped]
public class EnemyAttackResolver
{
    public const string AttackAction = "Attack";

    private readonly IDiceRoller _diceRoller;
    private readonly IDamageService _damageService;
    private readonly INpcService _npcService;

    public EnemyAttackResolver(IDiceRoller diceRoller, IDamageService damageService, INpcService npcService)
    {
        _diceRoller = diceRoller;
        _damageService = damageService;
        _npcService = npcService;
    }

    public async Task<EnemyAttackResult> ResolveAsync(EnemyAttackInput input)
    {
        var attacker = input.Attacker;
        if (attacker.CurrentHP == 0)
            return EnemyAttackResult.Failed($"{attacker.Name} is defeated, so it can't attack.");

        if ((input.Character is null) == (input.Npc is null))
            return EnemyAttackResult.Failed("Pick who it attacks: a player's character or an NPC.");

        var dice = Text(input.Damage) ?? attacker.Damage;
        if (dice is null)
            return EnemyAttackResult.Failed($"{attacker.Name} has no damage dice. Give it some with /dm encounter edit, or fill in damage.");

        if (!_diceRoller.TryRoll(dice, out var roll))
            return EnemyAttackResult.Failed($"`{dice}` is not a valid amount or dice roll.");

        string target, description;
        int taken;
        if (input.Character is { } character)
        {
            var hit = await _damageService.TakeAsync(character, roll.Value, input.Type, input.Piercing);
            (target, taken, description) = (character.Name, hit.Taken.Taken, hit.Describe(character));
        }
        else
        {
            var npc = input.Npc!;
            var hit = await _npcService.DamageAsync(npc, roll.Value, input.Type, input.Piercing);
            (target, taken, description) = (npc.Name, hit.Taken, NpcSheet.DescribeHit(npc, hit));
        }

        var action = Text(input.Move) ?? AttackAction;
        return new EnemyAttackResult(null, new EnemyAttack(
            $"⚔️ {attacker.Name}: {action}",
            $"**{attacker.Name}** attacks **{target}**! `{dice}` → **{roll.Value}**\n\n{description}",
            target,
            taken,
            new EncounterAction
            {
                Actor = attacker.Name,
                EnemyID = attacker.ID,
                Action = action,
                Roll = $"{dice} = {roll.Value}",
                Target = target,
                Result = $"💥 {taken} damage",
            }));
    }

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

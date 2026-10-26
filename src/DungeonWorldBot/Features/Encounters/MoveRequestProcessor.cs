using System.Drawing;
using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Infrastructure;
using DungeonWorldBot.Rules;
using Remora.Discord.API.Objects;
using Remora.Rest.Core;

namespace DungeonWorldBot.Features.Encounters;

/// <summary>
/// A move asked for from a scene, rolled: what to tell game chat, and what it did.
/// </summary>
/// <param name="Action">What it did, already in the fight's spotlight.</param>
/// <param name="Summary">A few words for the board to show over the target, e.g. "Miss" or "-4".</param>
/// <param name="Roller">The player whose 2d6 roll it was, to count in their roll stats. Null for an enemy's attack.</param>
/// <param name="Dice">The 2d6 roll, for roll stats.</param>
public record ProcessedMove(Embed Embed, EncounterAction Action, string Summary, Snowflake? Roller = null, DiceResult? Dice = null);

/// <param name="Error">Why it couldn't be made, or null when it was.</param>
public record ProcessedMoveResult(string? Error, ProcessedMove? Move)
{
    public static ProcessedMoveResult Failed(string error) => new(error, null);
}

/// <summary>
/// Rolls a move asked for from a scene on the dashboard (see <see cref="MoveRequest"/>), by the same rules as /move and
/// /dm encounter attack, and puts it in the fight's spotlight. Telling game chat is left to <see cref="MoveRequestWorker"/>.
/// </summary>
[Scoped]
public class MoveRequestProcessor
{
    private static readonly Color AttackColour = Color.FromArgb(0xD8, 0x5A, 0x30);
    private static readonly EmbedFooter FromScene = new("Rolled from the fight's scene");

    private readonly DungeonWorldContext _context;
    private readonly ICharacterService _characterService;
    private readonly IEncounterService _encounterService;
    private readonly MoveResolver _moveResolver;
    private readonly EnemyAttackResolver _enemyAttackResolver;

    public MoveRequestProcessor(
        DungeonWorldContext context,
        ICharacterService characterService,
        IEncounterService encounterService,
        MoveResolver moveResolver,
        EnemyAttackResolver enemyAttackResolver)
    {
        _context = context;
        _characterService = characterService;
        _encounterService = encounterService;
        _moveResolver = moveResolver;
        _enemyAttackResolver = enemyAttackResolver;
    }

    public async Task<ProcessedMoveResult> ProcessAsync(MoveRequest request)
    {
        if (await _context.Encounters.FindAsync(request.EncounterID) is not { EndedAt: null })
            return ProcessedMoveResult.Failed("The fight is over.");

        return request.Kind switch
        {
            MoveRequestKind.HackAndSlash => await PlayerMoveAsync(request, BasicMoves.HackAndSlash, StatType.Str),
            MoveRequestKind.Volley => await PlayerMoveAsync(request, BasicMoves.Volley, StatType.Dex),
            MoveRequestKind.EnemyAttack => await EnemyAttackAsync(request),
            _ => ProcessedMoveResult.Failed("The bot doesn't know that move."),
        };
    }

    private async Task<ProcessedMoveResult> PlayerMoveAsync(MoveRequest request, MoveDefinition move, StatType stat)
    {
        if (request.CharacterID is not { } characterID || await _characterService.GetCharacterFromUserAsync(characterID) is not { } character)
            return ProcessedMoveResult.Failed("That character isn't around anymore.");

        if (await TargetEnemyAsync(request) is not { } target)
            return ProcessedMoveResult.Failed("That enemy isn't in the fight anymore.");

        if (TacticRules.WhyCannotMake(character, move.Name) is { } forbidden)
            return ProcessedMoveResult.Failed(forbidden);

        if (MoveInput.ForStat(character, stat, request.Bonus, target.Name, request.ExtraDamage) is not { } input)
            return ProcessedMoveResult.Failed($"{character.Name} has no {stat} stat.");

        var resolution = await _moveResolver.ResolveAsync(character, move, input);
        var action = resolution.ToAction(messageID: null);
        await LogAsync(request, action);

        var outcome = resolution.Outcome switch
        {
            MoveOutcome.StrongHit => "Strong hit",
            MoveOutcome.WeakHit => "Weak hit",
            _ => "Miss",
        };
        var summary = resolution.Dealt?.Taken is { } taken ? $"{outcome} · -{taken}" : outcome;

        return new ProcessedMoveResult(null, new ProcessedMove(
            resolution.Embed with { Footer = FromScene },
            action,
            summary,
            character.ID,
            resolution.Roll.Dice));
    }

    private async Task<ProcessedMoveResult> EnemyAttackAsync(MoveRequest request)
    {
        if (request.EnemyID is not { } enemyID || await _context.Enemies.FindAsync(enemyID) is not { } attacker || attacker.EncounterID != request.EncounterID)
            return ProcessedMoveResult.Failed("That enemy isn't in the fight anymore.");

        Character? character = null;
        Npc? npc = null;
        if (request.TargetCharacterID is { } characterID)
            character = await _characterService.GetCharacterFromUserAsync(characterID);
        else if (request.TargetNpcID is { } npcID)
            npc = await _context.Npcs.FindAsync(npcID);
        if (character is null && npc is null)
            return ProcessedMoveResult.Failed("Who it attacked isn't around anymore.");

        var attacked = await _enemyAttackResolver.ResolveAsync(new EnemyAttackInput(attacker, character, npc, request.Move));
        if (attacked.Attack is not { } attack)
            return ProcessedMoveResult.Failed(attacked.Error!);

        await LogAsync(request, attack.Action);

        return new ProcessedMoveResult(null, new ProcessedMove(
            new Embed(Title: attack.Title, Description: attack.Description, Colour: AttackColour, Footer: FromScene),
            attack.Action,
            $"-{attack.Taken}"));
    }

    /// <summary>
    /// The enemy a player's move is aimed at, if it's still in the request's fight.
    /// </summary>
    private async Task<Enemy?> TargetEnemyAsync(MoveRequest request) =>
        request.TargetEnemyID is { } id && await _context.Enemies.FindAsync(id) is { } enemy && enemy.EncounterID == request.EncounterID
            ? enemy
            : null;

    private async Task LogAsync(MoveRequest request, EncounterAction action)
    {
        // In the fight of the enemy acting or aimed at, which is the request's own
        await _encounterService.LogAsync(action);
        request.ActionID = action.ID == 0 ? null : action.ID;
    }
}

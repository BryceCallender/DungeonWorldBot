using System.Drawing;
using d20Tek.DiceNotation.Results;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Features.Subclasses;
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
public class MoveRequestProcessor(
    DungeonWorldContext context,
    ICharacterService characterService,
    IEncounterService encounterService,
    MoveResolver moveResolver,
    CharacterMoves characterMoves,
    EnemyAttackResolver enemyAttackResolver)
{
    private static readonly Color AttackColour = Color.FromArgb(0xD8, 0x5A, 0x30);
    private static readonly EmbedFooter FromScene = new("Rolled from the fight's scene");

    public async Task<ProcessedMoveResult> ProcessAsync(MoveRequest request)
    {
        if (await context.Encounters.FindAsync(request.EncounterID) is not { EndedAt: null })
            return ProcessedMoveResult.Failed("The fight is over.");

        return request.Kind switch
        {
            MoveRequestKind.HackAndSlash or MoveRequestKind.Volley or MoveRequestKind.BasicMove or MoveRequestKind.ClassMove
                => await PlayerMoveAsync(request),
            MoveRequestKind.EnemyAttack => await EnemyAttackAsync(request),
            _ => ProcessedMoveResult.Failed("The bot doesn't know that move."),
        };
    }

    private async Task<ProcessedMoveResult> PlayerMoveAsync(MoveRequest request)
    {
        if (request.CharacterID is not { } characterID || await characterService.GetCharacterFromUserAsync(characterID) is not { } character)
            return ProcessedMoveResult.Failed("That character isn't around anymore.");

        Enemy? target = null;
        if (request.TargetEnemyID is not null && (target = await TargetEnemyAsync(request)) is null)
            return ProcessedMoveResult.Failed("That enemy isn't in the fight anymore.");

        Character? ally = null;
        if (request.TargetCharacterID is { } allyID && (ally = await characterService.GetCharacterFromUserAsync(allyID)) is null)
            return ProcessedMoveResult.Failed("Who you're helping isn't around anymore.");

        var prepared = request.Kind switch
        {
            MoveRequestKind.HackAndSlash => Basic(character, BasicMoves.HackAndSlash, null, null),
            MoveRequestKind.Volley => Basic(character, BasicMoves.Volley, null, null),
            MoveRequestKind.ClassMove => characterMoves.PrepareClassMove(character, request.Move ?? "", request.Stat),
            _ => BasicMoves.All.FirstOrDefault(m => m.Name.Equals(request.Move?.Trim(), StringComparison.OrdinalIgnoreCase)) is { } basic
                ? Basic(character, basic, request.Stat, ally)
                : PreparedMove.Failed($"There's no basic move called {request.Move}."),
        };
        if (prepared.Error is { } error)
            return ProcessedMoveResult.Failed(error);

        var move = prepared.Move!;
        if (TacticRules.WhyCannotMake(character, move.Name) is { } forbidden)
            return ProcessedMoveResult.Failed(forbidden);

        var input = prepared.Input! with { Bonus = request.Bonus, Target = target?.Name, ExtraDamage = request.ExtraDamage };
        var resolution = await moveResolver.ResolveAsync(character, move, input);
        var action = resolution.ToAction(messageID: null, ally?.Name);
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

    /// <summary>
    /// What a basic move rolls with, as /move rolls it.
    /// </summary>
    /// <param name="stat">The stat they picked, for a move like Defy Danger that rolls whichever fits.</param>
    /// <param name="ally">Who they're helping or hindering, for Aid or Interfere.</param>
    private static PreparedMove Basic(Character character, MoveDefinition move, StatType? stat, Character? ally)
    {
        if (move == BasicMoves.AidOrInterfere)
        {
            if (ally is null)
                return PreparedMove.Failed("Pick whose character you're helping or hindering.");
            var bonds = character.Bonds?.Count(b => b.TargetID == ally.ID) ?? 0;
            return new PreparedMove(null, move, new MoveInput($"Bond with {ally.Name}", bonds, 0));
        }

        if (move == BasicMoves.LastBreath)
            return new PreparedMove(null, move, new MoveInput(null, 0, 0, Kind: RollKind.LastBreath));

        if ((stat ?? move.Stat) is not { } rolled)
            return PreparedMove.Failed($"Pick a stat to roll {move.Name} with.");

        return MoveInput.ForStat(character, rolled, 0) is { } input
            ? new PreparedMove(null, move, input)
            : PreparedMove.Failed($"{character.Name} has no {rolled} stat.");
    }

    private async Task<ProcessedMoveResult> EnemyAttackAsync(MoveRequest request)
    {
        if (request.EnemyID is not { } enemyID || await context.Enemies.FindAsync(enemyID) is not { } attacker || attacker.EncounterID != request.EncounterID)
            return ProcessedMoveResult.Failed("That enemy isn't in the fight anymore.");

        Character? character = null;
        Npc? npc = null;
        if (request.TargetCharacterID is { } characterID)
            character = await characterService.GetCharacterFromUserAsync(characterID);
        else if (request.TargetNpcID is { } npcID)
            npc = await context.Npcs.FindAsync(npcID);
        if (character is null && npc is null)
            return ProcessedMoveResult.Failed("Who it attacked isn't around anymore.");

        var attacked = await enemyAttackResolver.ResolveAsync(new EnemyAttackInput(attacker, character, npc, request.Move));
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
        request.TargetEnemyID is { } id && await context.Enemies.FindAsync(id) is { } enemy && enemy.EncounterID == request.EncounterID
            ? enemy
            : null;

    private async Task LogAsync(MoveRequest request, EncounterAction action)
    {
        // In the fight of the enemy acting or aimed at, which is the request's own
        await encounterService.LogAsync(action);
        request.ActionID = action.ID == 0 ? null : action.ID;
    }
}

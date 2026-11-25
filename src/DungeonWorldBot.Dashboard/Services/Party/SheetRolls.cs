using DungeonWorldBot.Dashboard.Models.Auth;
using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Dashboard.Models.Party;
using DungeonWorldBot.Dashboard.Services.Encounters;
using DungeonWorldBot.Dashboard.Services.Shared;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Services.Party;

/// <summary>
/// Moves rolled from a character's sheet, for players who'd rather tap a button than remember /move. Like a move made
/// from a fight's board, the bot rolls it (see <see cref="MoveRequest"/>) and posts it in game chat, where it counts
/// toward roll stats and tumbles onto every open page.
/// </summary>
/// <remarks>
/// Players roll the moves of the character they're playing: their own, or one they've taken over. With sign-in off,
/// anyone can roll anyone's, as on the board.
/// </remarks>
public class SheetRolls(ICharacterService characters, RollableMoves rollable, EncounterDesk desk, DungeonWorldContext context, TimeProvider time)
{
    // A little after the bot itself gives up, so a roll it never got to (as while it's off) doesn't wait forever
    private static readonly TimeSpan GiveUpAfter = MoveRequestRules.GiveUpAfter + TimeSpan.FromSeconds(10);
    private const string BotAway = "The bot didn't roll it. Is it running?";

    /// <summary>
    /// Whether the viewer plays the character: it's the one the bot would roll their /move for (see
    /// <see cref="PlayedCharacters"/>), or anyone's with sign-in off.
    /// </summary>
    public async Task<bool> CanRollAsync(Viewer viewer, ulong characterID) =>
        viewer is not { SignedIn: true, ID: { } me }
        || PlayedCharacters.Of(await characters.GetCharacterNamesAsync(), me).Plays(characterID);

    public async Task<MoveAsked> AskAsync(ulong characterID, Viewer viewer, SheetRoll roll)
    {
        if (!await CanRollAsync(viewer, characterID))
            return MoveAsked.Failed("You can only roll as your own character.");
        if (Math.Abs(roll.Bonus) > FightMoves.MaxBonus)
            return MoveAsked.Failed($"Keep bonuses between -{FightMoves.MaxBonus} and +{FightMoves.MaxBonus}.");
        if (await characters.GetCharacterFromUserAsync(new Snowflake(characterID)) is not { } character)
            return MoveAsked.Failed("That character isn't around anymore.");
        if (MoveOf(character, roll) is not { } move)
            return MoveAsked.Failed($"{character.Name} can't make {roll.Move}.");
        if (WhyNot(move, roll, characterID) is { } problem)
            return MoveAsked.Failed(problem);

        return await desk.RequestMoveAsync(new MoveRequest
        {
            RequestedBy = viewer.ID is { } id ? new Snowflake(id) : null,
            Kind = move.Kind,
            CharacterID = character.ID,
            Move = move.Name,
            Stat = move.PicksStat ? roll.Stat : null,
            TargetCharacterID = move.Aim == BoardAim.Ally ? new Snowflake(roll.Ally!.Value) : null,
            Bonus = roll.Bonus,
        });
    }

    /// <summary>
    /// How a roll asked for from the character's sheet went, or null if there's no such roll of theirs.
    /// </summary>
    public async Task<SheetRollStatus?> StatusAsync(ulong characterID, int requestID)
    {
        var character = new Snowflake(characterID);
        var request = await context.MoveRequests.AsNoTracking()
            .Where(r => r.ID == requestID && r.CharacterID == character && r.EncounterID == null)
            .Select(r => new { r.RequestedAt, r.ResolvedAt, r.Summary, r.Error })
            .FirstOrDefaultAsync();

        return request switch
        {
            null => null,
            // The bot marks it taken before rolling it, and only says how it went once it's rolled
            { ResolvedAt: not null } when request.Summary is not null || request.Error is not null => new SheetRollStatus(true, request.Summary, request.Error),
            _ when time.GetUtcNow() - request.RequestedAt > GiveUpAfter => new SheetRollStatus(true, Error: BotAway),
            _ => SheetRollStatus.Waiting,
        };
    }

    private BoardMove? MoveOf(Character character, SheetRoll roll)
    {
        var kind = roll.ClassMove ? MoveRequestKind.ClassMove : MoveRequestKind.BasicMove;
        return rollable.For(character).FirstOrDefault(m => m.Kind == kind && m.Name.Equals(roll.Move.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string? WhyNot(BoardMove move, SheetRoll roll, ulong characterID)
    {
        if (move.PicksStat && roll.Stat is null)
            return $"Pick a stat to roll {move.Name} with.";
        if (move.Aim == BoardAim.Ally && (roll.Ally is not { } ally || ally == characterID))
            return "Pick whose character you're helping or hindering.";
        return null;
    }
}

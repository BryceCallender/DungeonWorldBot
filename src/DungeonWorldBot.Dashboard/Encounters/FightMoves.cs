using System.Text.Json;
using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Encounters;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// A move someone made from a token on a fight's board, at another token.
/// </summary>
/// <param name="Actor">The token making it, by its item ID.</param>
/// <param name="Target">The token it's aimed at, by its item ID.</param>
/// <param name="Move">What an enemy does, e.g. Bite. A plain attack if not given.</param>
public record MoveAsk(MoveRequestKind Kind, string Actor, string Target, int Bonus = 0, int ExtraDamage = 0, string? Move = null);

/// <param name="Error">Why it wasn't asked for, or null when it was.</param>
/// <param name="ID">The request the bot will roll.</param>
public record MoveAsked(string? Error, int ID = 0)
{
    public static MoveAsked Failed(string error) => new(error);
}

/// <summary>
/// Moves made from tokens on a fight's board: works out who the tokens are and whether the viewer can make the move,
/// then asks the bot to roll it (see <see cref="MoveRequest"/>).
/// </summary>
/// <remarks>
/// Players make their own character's moves, and whoever runs fights (see <see cref="EncounterEndpoints.CanRun"/>)
/// makes the enemies'. With sign-in off, anyone can do either, as with the rest of the fight.
/// </remarks>
public class FightMoves(CanvasStore store, IEncounterService encounters, DungeonWorldContext context, EncounterDesk desk)
{
    public const int MaxBonus = 10;
    public const int MaxMoveLength = 80;

    public async Task<MoveAsked> AskAsync(int canvasID, Viewer viewer, MoveAsk ask)
    {
        if (await store.FindAsync(canvasID) is not { Kind: CanvasKind.Encounter } scene
            || await encounters.GetOnSceneAsync(CanvasIds.Encode(scene.ID)) is not { } fight)
            return MoveAsked.Failed("There's no fight going on here.");

        if (Math.Abs(ask.Bonus) > MaxBonus || Math.Abs(ask.ExtraDamage) > MaxBonus)
            return MoveAsked.Failed($"Keep bonuses between -{MaxBonus} and +{MaxBonus}.");

        if (await FighterAsync(canvasID, ask.Actor, fight) is not { } actor)
            return MoveAsked.Failed("That token isn't anyone in the fight.");
        if (await FighterAsync(canvasID, ask.Target, fight) is not { } target)
            return MoveAsked.Failed("Pick a token that's in the fight to aim at.");

        var request = new MoveRequest
        {
            EncounterID = fight.ID,
            RequestedBy = viewer.ID is { } id ? new Snowflake(id) : null,
            Kind = ask.Kind,
            Bonus = ask.Bonus,
            ExtraDamage = ask.ExtraDamage,
        };

        var problem = ask.Kind switch
        {
            MoveRequestKind.HackAndSlash or MoveRequestKind.Volley => PlayerMove(fight, viewer, actor, target, request),
            MoveRequestKind.EnemyAttack => EnemyAttack(fight, viewer, actor, target, ask, request),
            _ => "That isn't a move you can make from the board yet.",
        };
        return problem is null ? await desk.RequestMoveAsync(request) : MoveAsked.Failed(problem);
    }

    private static string? PlayerMove(Encounter fight, Viewer viewer, TokenFighter actor, TokenFighter target, MoveRequest request)
    {
        if (actor.Kind != FighterKind.Character)
            return "Only a player's character can make that move.";

        var character = ulong.Parse(actor.ID);
        if (viewer.SignedIn && !viewer.Is(character))
            return "You can only make moves as your own character.";
        if (!InFight(fight, character))
            return "They aren't in this fight.";
        if (EnemyIn(fight, target) is not { } enemy)
            return "Aim it at an enemy in this fight.";
        if (enemy.CurrentHP == 0)
            return $"{enemy.Name} is already defeated.";

        request.CharacterID = new Snowflake(character);
        request.TargetEnemyID = enemy.ID;
        return null;
    }

    private static string? EnemyAttack(Encounter fight, Viewer viewer, TokenFighter actor, TokenFighter target, MoveAsk ask, MoveRequest request)
    {
        if (!EncounterEndpoints.CanRun(viewer))
            return "Only the DM can attack as an enemy.";
        if (EnemyIn(fight, actor) is not { } enemy)
            return "Only an enemy in this fight can attack.";
        if (enemy.CurrentHP == 0)
            return $"{enemy.Name} is defeated, so it can't attack.";

        switch (target.Kind)
        {
            case FighterKind.Character when InFight(fight, ulong.Parse(target.ID)):
                request.TargetCharacterID = new Snowflake(ulong.Parse(target.ID));
                break;
            case FighterKind.Npc when int.TryParse(target.ID, out var npc) && NpcInFight(fight, npc):
                request.TargetNpcID = npc;
                break;
            default:
                return "Aim it at a player's character or an NPC in this fight.";
        }

        request.EnemyID = enemy.ID;
        request.Move = string.IsNullOrWhiteSpace(ask.Move) ? null : ask.Move.Trim()[..Math.Min(ask.Move.Trim().Length, MaxMoveLength)];
        return null;
    }

    // A fight the DM didn't pick anyone for (started in Discord) is everyone's
    private static bool InFight(Encounter fight, ulong character) =>
        fight.Participants.Count == 0 || fight.Participants.Any(p => p.CharacterID is { } id && id.Value == character);

    private static bool NpcInFight(Encounter fight, int npc) =>
        fight.Participants.Count == 0 || fight.Participants.Any(p => p.NpcID == npc);

    private static Enemy? EnemyIn(Encounter fight, TokenFighter fighter) =>
        fighter.Kind == FighterKind.Enemy && int.TryParse(fighter.ID, out var id) ? fight.Enemies.FirstOrDefault(e => e.ID == id) : null;

    /// <summary>
    /// Who a token is: from its fighter attribute if it has one, else by its name, as for tokens made by hand.
    /// </summary>
    private async Task<TokenFighter?> FighterAsync(int canvasID, string itemID, Encounter fight)
    {
        if (await store.FindItemAsync(canvasID, itemID) is not { Type: CanvasItemType.Token } item)
            return null;

        using var json = JsonDocument.Parse(item.Json);
        if (!json.RootElement.TryGetProperty("attrs", out var attrs))
            return null;
        if (attrs.TryGetProperty(TokenFighter.Attribute, out var tagged) && TokenFighter.Parse(tagged.GetString()) is { } fighter)
            return fighter;
        if (!attrs.TryGetProperty("name", out var named) || named.GetString()?.Trim() is not { Length: > 0 } name)
            return null;

        if (fight.Enemies.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } enemy)
            return TokenFighter.Enemy(enemy.ID);

        var lower = name.ToLower();
        if (await context.Characters.AsNoTracking().Where(c => c.Name.ToLower() == lower).Select(c => (Snowflake?)c.ID).FirstOrDefaultAsync() is { } character)
            return TokenFighter.Character(character.Value);

        // NPCs' names are matched however they're typed
        return await context.Npcs.AsNoTracking().Where(n => n.Name == name).Select(n => (int?)n.ID).FirstOrDefaultAsync() is { } npc
            ? TokenFighter.Npc(npc)
            : null;
    }
}

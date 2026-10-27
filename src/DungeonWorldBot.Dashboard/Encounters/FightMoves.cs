using System.Text.Json;
using DungeonWorldBot.Dashboard.Auth;
using DungeonWorldBot.Dashboard.Canvases;
using DungeonWorldBot.Data;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Characters;
using DungeonWorldBot.Features.Encounters;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Rules;
using Microsoft.EntityFrameworkCore;
using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Encounters;

/// <summary>
/// A move someone made from a token on a fight's board, at another token.
/// </summary>
/// <param name="Actor">The token making it, by its item ID.</param>
/// <param name="Target">The token it's aimed at, by its item ID, or null for a move like Spout Lore.</param>
/// <param name="Move">What an enemy does, e.g. Bite (a plain attack if not given), or the player's move, e.g. Defy Danger.</param>
/// <param name="Stat">The stat a player picked to roll with, for a move like Defy Danger.</param>
public record MoveAsk(
    MoveRequestKind Kind,
    string Actor,
    string? Target = null,
    int Bonus = 0,
    int ExtraDamage = 0,
    string? Move = null,
    StatType? Stat = null);

/// <summary>
/// Who a move made from the board is aimed at.
/// </summary>
public enum BoardAim
{
    None,
    Enemy,

    /// <summary>
    /// Another player's character, to help or hinder.
    /// </summary>
    Ally,

    /// <summary>
    /// A player's character or an NPC, for an enemy's attack.
    /// </summary>
    Party,
}

/// <summary>
/// A move a token can make, for its right-click menu.
/// </summary>
/// <param name="PicksStat">Whether the player chooses the stat it rolls with, like Defy Danger.</param>
/// <param name="Modifier">What the roll adds to 2d6 before any bonus, for showing the chance to hit. Null when it's not
/// known until the player picks (see <paramref name="StatModifiers"/>), or for an enemy's attack, which isn't 2d6.</param>
/// <param name="StatModifiers">Each stat's modifier by its short name ("str"), for a move that <paramref name="PicksStat"/>.</param>
public record BoardMove(
    MoveRequestKind Kind,
    string Name,
    BoardAim Aim,
    bool PicksStat = false,
    bool RollsDamage = false,
    int? Modifier = null,
    IReadOnlyDictionary<string, int>? StatModifiers = null);

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
/// Players make their own character's moves (any of them, see <see cref="CharacterMoves"/>), and whoever runs fights (see <see cref="EncounterEndpoints.CanRun"/>)
/// makes the enemies'. With sign-in off, anyone can do either, as with the rest of the fight.
/// </remarks>
public class FightMoves(CanvasStore store, IEncounterService encounters, ICharacterService characters, DungeonWorldContext context, EncounterDesk desk, CharacterMoves characterMoves)
{
    public const int MaxBonus = 10;
    public const int MaxMoveLength = 80;

    /// <summary>
    /// The moves the viewer can make from a token: all of their own character's, or an enemy's attack if they run the
    /// fight. None for anyone else's.
    /// </summary>
    public async Task<IReadOnlyList<BoardMove>> MovesForAsync(int canvasID, Viewer viewer, string itemID)
    {
        if (await store.FindAsync(canvasID) is not { Kind: CanvasKind.Encounter } scene
            || await encounters.GetOnSceneAsync(CanvasIds.Encode(scene.ID)) is not { } fight
            || await FighterAsync(canvasID, itemID, fight) is not { } actor)
            return [];

        switch (actor.Kind)
        {
            case FighterKind.Character when OwnCharacter(fight, viewer, actor) is { } id
                                           && await characters.GetCharacterFromUserAsync(new Snowflake(id)) is { } character:
                var stats = character.Stats.ToDictionary(s => s.StatType.ToString().ToLowerInvariant(), s => s.Modifier + Penalty(character, s.StatType));
                return CharacterMoves.For(character).Select(m => new BoardMove(
                    m.IsClassMove ? MoveRequestKind.ClassMove : MoveRequestKind.BasicMove,
                    m.Name,
                    m.Aim switch { MoveAim.Enemy => BoardAim.Enemy, MoveAim.Ally => BoardAim.Ally, _ => BoardAim.None },
                    m.PicksStat,
                    m.RollsDamage,
                    m.PicksStat ? null : ModifierFor(character, m),
                    m.PicksStat ? stats : null)).ToList();
            case FighterKind.Enemy when EncounterEndpoints.CanRun(viewer) && EnemyIn(fight, actor) is { CurrentHP: > 0 }:
                return [new BoardMove(MoveRequestKind.EnemyAttack, "Attack", BoardAim.Party)];
            default:
                return [];
        }
    }

    // What a move adds to its 2d6, worked out as the bot will when it rolls it, debilities and all
    private int? ModifierFor(Character character, CharacterMove move)
    {
        var input = move.IsClassMove
            ? characterMoves.PrepareClassMove(character, move.Name).Input
            : BasicMoves.All.FirstOrDefault(b => b.Name == move.Name) is { Stat: { } stat } ? MoveInput.ForStat(character, stat, 0) : null;

        return input is null ? null : input.Modifier + (input.Stat is { } rolled ? Penalty(character, rolled) : 0);
    }

    private static int Penalty(Character character, StatType stat) => DebilityRules.RollPenalty(character, stat)?.Penalty ?? 0;

    public async Task<MoveAsked> AskAsync(int canvasID, Viewer viewer, MoveAsk ask)
    {
        if (await store.FindAsync(canvasID) is not { Kind: CanvasKind.Encounter } scene
            || await encounters.GetOnSceneAsync(CanvasIds.Encode(scene.ID)) is not { } fight)
            return MoveAsked.Failed("There's no fight going on here.");

        if (Math.Abs(ask.Bonus) > MaxBonus || Math.Abs(ask.ExtraDamage) > MaxBonus)
            return MoveAsked.Failed($"Keep bonuses between -{MaxBonus} and +{MaxBonus}.");

        if (await FighterAsync(canvasID, ask.Actor, fight) is not { } actor)
            return MoveAsked.Failed("That token isn't anyone in the fight.");
        TokenFighter? target = null;
        if (ask.Target is not null && (target = await FighterAsync(canvasID, ask.Target, fight)) is null)
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
            MoveRequestKind.EnemyAttack => EnemyAttack(fight, viewer, actor, target, ask, request),
            _ => await PlayerMoveAsync(fight, viewer, actor, target, ask, request),
        };
        return problem is null ? await desk.RequestMoveAsync(request) : MoveAsked.Failed(problem);
    }

    private async Task<string?> PlayerMoveAsync(Encounter fight, Viewer viewer, TokenFighter actor, TokenFighter? target, MoveAsk ask, MoveRequest request)
    {
        if (actor.Kind != FighterKind.Character)
            return "Only a player's character can make that move.";
        if (OwnCharacter(fight, viewer, actor) is not { } id)
            return viewer.SignedIn && !viewer.Is(actor.ID) ? "You can only make moves as your own character." : "They aren't in this fight.";
        if (await characters.GetCharacterFromUserAsync(new Snowflake(id)) is not { } character)
            return "That character isn't around anymore.";

        var name = ask.Kind switch
        {
            MoveRequestKind.HackAndSlash => BasicMoves.HackAndSlash.Name,
            MoveRequestKind.Volley => BasicMoves.Volley.Name,
            _ => ask.Move?.Trim(),
        };
        var isClassMove = ask.Kind == MoveRequestKind.ClassMove;
        if (CharacterMoves.For(character).FirstOrDefault(m => m.IsClassMove == isClassMove && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is not { } move)
            return $"{character.Name} can't make {name ?? "that move"}.";

        switch (move.Aim)
        {
            case MoveAim.Enemy:
                if (target is null || EnemyIn(fight, target) is not { } enemy)
                    return "Aim it at an enemy in this fight.";
                if (enemy.CurrentHP == 0)
                    return $"{enemy.Name} is already defeated.";
                request.TargetEnemyID = enemy.ID;
                break;
            case MoveAim.Ally:
                if (target is not { Kind: FighterKind.Character } || target.ID == actor.ID || !InFight(fight, ulong.Parse(target.ID)))
                    return "Aim it at another player's character in this fight.";
                request.TargetCharacterID = new Snowflake(ulong.Parse(target.ID));
                break;
        }

        if (move.PicksStat && ask.Stat is null)
            return $"Pick a stat to roll {move.Name} with.";

        request.Kind = isClassMove ? MoveRequestKind.ClassMove : MoveRequestKind.BasicMove;
        request.CharacterID = new Snowflake(id);
        request.Move = move.Name;
        request.Stat = move.PicksStat ? ask.Stat : null;
        return null;
    }

    private static string? EnemyAttack(Encounter fight, Viewer viewer, TokenFighter actor, TokenFighter? target, MoveAsk ask, MoveRequest request)
    {
        if (!EncounterEndpoints.CanRun(viewer))
            return "Only the DM can attack as an enemy.";
        if (EnemyIn(fight, actor) is not { } enemy)
            return "Only an enemy in this fight can attack.";
        if (enemy.CurrentHP == 0)
            return $"{enemy.Name} is defeated, so it can't attack.";

        switch (target?.Kind)
        {
            case FighterKind.Character when InFight(fight, ulong.Parse(target!.ID)):
                request.TargetCharacterID = new Snowflake(ulong.Parse(target!.ID));
                break;
            case FighterKind.Npc when int.TryParse(target!.ID, out var npc) && NpcInFight(fight, npc):
                request.TargetNpcID = npc;
                break;
            default:
                return "Aim it at a player's character or an NPC in this fight.";
        }

        request.EnemyID = enemy.ID;
        request.Move = string.IsNullOrWhiteSpace(ask.Move) ? null : ask.Move.Trim()[..Math.Min(ask.Move.Trim().Length, MaxMoveLength)];
        return null;
    }

    /// <summary>
    /// The character a token is, if it's the viewer's own (anyone's, with sign-in off) and they're in the fight.
    /// </summary>
    private static ulong? OwnCharacter(Encounter fight, Viewer viewer, TokenFighter actor) =>
        actor.Kind == FighterKind.Character && ulong.TryParse(actor.ID, out var id) && (!viewer.SignedIn || viewer.Is(id)) && InFight(fight, id)
            ? id
            : null;

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

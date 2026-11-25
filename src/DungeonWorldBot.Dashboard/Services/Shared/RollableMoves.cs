using DungeonWorldBot.Dashboard.Models.Encounters;
using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Moves;
using DungeonWorldBot.Rules;

namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// The moves a character can roll from the dashboard, with what each adds to its 2d6, for a token's menu on a fight's
/// board and the roll buttons on their sheet alike.
/// </summary>
public class RollableMoves(CharacterMoves characterMoves)
{
    /// <summary>
    /// Every move they can make (see <see cref="CharacterMoves.For"/>), worked out as the bot will roll it.
    /// </summary>
    public IReadOnlyList<BoardMove> For(Character character)
    {
        var stats = character.Stats.ToDictionary(s => s.StatType.ToString().ToLowerInvariant(), s => s.Modifier + Penalty(character, s.StatType));
        return CharacterMoves.For(character).Select(m => new BoardMove(
            m.IsClassMove ? MoveRequestKind.ClassMove : MoveRequestKind.BasicMove,
            m.Name,
            AimOf(m.Aim),
            m.PicksStat,
            m.RollsDamage,
            m.PicksStat ? null : ModifierFor(character, m),
            m.PicksStat ? stats : null)).ToList();
    }

    private static BoardAim AimOf(MoveAim aim) => aim switch
    {
        MoveAim.Enemy => BoardAim.Enemy,
        MoveAim.Ally => BoardAim.Ally,
        _ => BoardAim.None,
    };

    // What a move adds to its 2d6, worked out as the bot will when it rolls it, debilities and all
    private int? ModifierFor(Character character, CharacterMove move)
    {
        if (InputFor(character, move) is not { } input)
            return null;

        var penalty = input.Stat is { } rolled ? Penalty(character, rolled) : 0;
        return input.Modifier + penalty;
    }

    private MoveInput? InputFor(Character character, CharacterMove move)
    {
        if (move.IsClassMove)
            return characterMoves.PrepareClassMove(character, move.Name) is ReadyMove ready ? ready.Input : null;

        return BasicMoves.All.FirstOrDefault(b => b.Name == move.Name) is { Stat: { } stat } ? MoveInput.ForStat(character, stat, 0) : null;
    }

    private static int Penalty(Character character, StatType stat) => DebilityRules.RollPenalty(character, stat)?.Penalty ?? 0;
}

using DungeonWorldBot.Data.Entities;
using DungeonWorldBot.Features.Classes;
using DungeonWorldBot.Features.Resources;
using DungeonWorldBot.Features.Subclasses;
using DungeonWorldBot.Infrastructure;

namespace DungeonWorldBot.Features.Moves;

/// <summary>
/// Who a move is aimed at, if anyone.
/// </summary>
public enum MoveAim
{
    None,

    /// <summary>
    /// An enemy, to take the move's damage off.
    /// </summary>
    Enemy,

    /// <summary>
    /// Another player's character, to help or hinder.
    /// </summary>
    Ally,
}

/// <summary>
/// A move a character can make right now.
/// </summary>
/// <param name="IsClassMove">One of their class or subclass moves, rolled like /move class, rather than a basic move.</param>
/// <param name="PicksStat">Whether they choose the stat it rolls with, like Defy Danger.</param>
public record CharacterMove(string Name, bool IsClassMove, MoveAim Aim, bool PicksStat = false, bool RollsDamage = false);

/// <summary>
/// A move worked out for a character, ready to roll with any bonus, target and extra damage added, or why it can't be.
/// </summary>
public record PreparedMove(string? Error, MoveDefinition? Move = null, MoveInput? Input = null)
{
    public static PreparedMove Failed(string error) => new(error);
}

/// <summary>
/// The moves a character can make, and what their class moves roll with. Shared by /move and the dashboard's board.
/// </summary>
[Scoped]
public class CharacterMoves(IResourceService resourceService)
{
    /// <summary>
    /// Every basic move, then their rolled class moves. Last Breath only once they're at 0 HP.
    /// </summary>
    public static IReadOnlyList<CharacterMove> For(Character character)
    {
        var basic = BasicMoves.All
            .Where(m => m != BasicMoves.LastBreath || character.Health.CurrentHP <= 0)
            .Select(m => new CharacterMove(
                m.Name,
                IsClassMove: false,
                m.RollsDamage ? MoveAim.Enemy : m == BasicMoves.AidOrInterfere ? MoveAim.Ally : MoveAim.None,
                PicksStat: m == BasicMoves.DefyDanger,
                m.RollsDamage));

        var known = ClassSheetLibrary.KnownMoves(character);
        var classMoves = RolledClassMoves(character)
            .Where(f => known.Contains(f.Move.Name, StringComparer.OrdinalIgnoreCase))
            .Select(f => new CharacterMove(
                f.Move.Name,
                IsClassMove: true,
                f.Move.Roll!.RollsDamage ? MoveAim.Enemy : MoveAim.None,
                RollsDamage: f.Move.Roll.RollsDamage));

        return basic.Concat(classMoves).DistinctBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// A rolled class, subclass or other class's move the character knows, with what it rolls with.
    /// </summary>
    /// <param name="stat">A different stat to roll with than usual, if the move lets them.</param>
    public PreparedMove PrepareClassMove(Character character, string move, StatType? stat = null)
    {
        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return PreparedMove.Failed($"The {character.Class.Type} doesn't have a class sheet in the bot yet.");

        var classMove = sheet.FindMove(move);
        var rollsFrom = sheet;
        if (classMove is null
            && Multiclassing.KnownForeignMoves(character, sheet).FirstOrDefault(f => f.Move.Name.Equals(move.Trim(), StringComparison.OrdinalIgnoreCase)) is { } foreign)
        {
            // A move learned from another class's sheet
            classMove = foreign.Move;
            rollsFrom = foreign.Sheet;
        }

        classMove ??= SubclassLibrary.Of(character)?.FindMove(move);

        if (classMove?.Roll is not { } definition)
            return PreparedMove.Failed($"{character.Name} doesn't have a rolled class move called {move}.");

        if (!ClassSheetLibrary.KnownMoves(character).Contains(classMove.Name, StringComparer.OrdinalIgnoreCase))
            return PreparedMove.Failed($"{character.Name} doesn't know {classMove.Name}. Learn it with /class learn");

        if (stat is null && classMove.RollsWith is { } rollsWith)
        {
            if (rollsFrom.FindResource(rollsWith) is { } resource)
                return new PreparedMove(null, definition, new MoveInput(resource.Name, resourceService.GetValue(character, resource), 0));

            if (rollsFrom.ClassStat?.Name.Equals(rollsWith, StringComparison.OrdinalIgnoreCase) == true)
            {
                stat = rollsFrom == sheet ? ClassSheetLibrary.ResolveClassStat(character, sheet) : null;
                if (stat is null)
                {
                    return PreparedMove.Failed(rollsFrom == sheet
                        ? $"Choose your {sheet.ClassStat.Feature} with /class learn first, it decides your {rollsWith} stat."
                        : $"{classMove.Name} rolls the {rollsFrom.Type}'s {rollsWith} stat. Pick which stat to use with the stat option.");
                }
            }
        }

        if ((stat ?? definition.Stat) is not { } statType)
            return new PreparedMove(null, definition, new MoveInput(null, 0, 0));

        return MoveInput.ForStat(character, statType, 0) is { } input
            ? new PreparedMove(null, definition, input)
            : PreparedMove.Failed($"Your character has no {statType} stat.");
    }

    // Their own class's, their subclass's, and any they learned from other classes
    private static IEnumerable<ForeignMove> RolledClassMoves(Character character)
    {
        if (ClassSheetLibrary.For(character.Class.Type) is not { } sheet)
            return [];

        return sheet.AllMoves.Select(m => new ForeignMove(sheet, m))
            .Concat((SubclassLibrary.Of(character)?.MovesAt(character.Level) ?? []).Select(m => new ForeignMove(sheet, m)))
            .Concat(Multiclassing.KnownForeignMoves(character, sheet))
            .Where(f => f.Move.Roll is not null);
    }
}

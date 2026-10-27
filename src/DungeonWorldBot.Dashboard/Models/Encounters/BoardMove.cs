using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

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

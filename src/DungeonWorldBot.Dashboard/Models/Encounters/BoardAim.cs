namespace DungeonWorldBot.Dashboard.Models.Encounters;

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

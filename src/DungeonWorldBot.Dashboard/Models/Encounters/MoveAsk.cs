using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Encounters;

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

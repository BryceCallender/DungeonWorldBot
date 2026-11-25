using DungeonWorldBot.Data.Entities;

namespace DungeonWorldBot.Dashboard.Models.Party;

/// <summary>
/// A move a player rolled from their character's sheet, outside any fight's board.
/// </summary>
/// <param name="Move">The move's name, e.g. Defy Danger.</param>
/// <param name="ClassMove">Whether it's one of their class or subclass moves, rather than a basic move.</param>
/// <param name="Stat">The stat they picked to roll with, for a move like Defy Danger.</param>
/// <param name="Ally">Whose character they're helping or hindering, for Aid or Interfere.</param>
/// <param name="Bonus">Any +forward, ongoing bonus or penalty.</param>
public record SheetRoll(string Move, bool ClassMove, StatType? Stat = null, ulong? Ally = null, int Bonus = 0);

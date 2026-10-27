using DungeonWorldBot.Dashboard.Models.Party;
using DungeonWorldBot.Features.Classes;

namespace DungeonWorldBot.Dashboard.Helpers;

public static class PartyLookups
{
    /// <summary>
    /// The character a Discord user plays, since characters share their player's ID.
    /// </summary>
    public static CharacterView? PlayedBy(this IEnumerable<CharacterView> party, ulong userID) =>
        party.FirstOrDefault(c => c.Character.ID.Value == userID);

    /// <summary>
    /// Whether a character already has a move, by the names from <see cref="ClassSheetLibrary.KnownMoves"/>.
    /// </summary>
    public static bool Knows(this IReadOnlyCollection<string> known, ClassMove move) =>
        known.Contains(move.Name, StringComparer.OrdinalIgnoreCase);
}

using Remora.Rest.Core;

namespace DungeonWorldBot.Dashboard.Models.Party;

/// <summary>
/// What changed on one character over a stretch of time, like a session: levels, moves, gear, bonds and the rest.
/// </summary>
/// <param name="Entries">Oldest first.</param>
public record CharacterChanges(Snowflake CharacterID, IReadOnlyList<HistoryEntry> Entries);

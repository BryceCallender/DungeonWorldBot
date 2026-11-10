namespace DungeonWorldBot.Dashboard.Models.Sessions;

/// <summary>
/// A stop the DM added to a session's journey by hand, for somewhere the party went without a fight to show it.
/// </summary>
/// <param name="ID">Made when it's added, for taking it away again.</param>
/// <param name="Who">The characters who went there, by Discord ID.</param>
public record LoggedStop(string ID, string Place, DateTimeOffset At, IReadOnlyList<ulong> Who);

namespace DungeonWorldBot.Dashboard.Models.Health;

/// <summary>
/// One of the databases on disk.
/// </summary>
/// <param name="JournalBytes">Its -wal file, holding writes not yet folded into the database.</param>
/// <param name="LastWrite">When it or its journal last changed, or null if it isn't there.</param>
public record DatabaseFile(string Name, string Path, long Bytes, long JournalBytes, DateTimeOffset? LastWrite);

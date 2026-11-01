namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// What a typed SQL statement gave back: the rows it read, or how many it changed, or what went wrong.
/// </summary>
/// <param name="Truncated">Whether there were more rows than are shown.</param>
public record AdminQueryResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    int? Changed,
    bool Truncated,
    string? Error)
{
    public static AdminQueryResult Failed(string error) => new([], [], null, false, error);
}

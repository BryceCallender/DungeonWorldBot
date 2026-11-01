namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// One page of a table's rows, after searching and sorting.
/// </summary>
/// <param name="Matching">How many rows match the search, across every page.</param>
public record AdminTableRows(string Table, IReadOnlyList<AdminColumn> Columns, IReadOnlyList<AdminRow> Rows, long Matching, int Page, int PageSize)
{
    public int Pages => (int)Math.Max(1, (Matching + PageSize - 1) / PageSize);
}

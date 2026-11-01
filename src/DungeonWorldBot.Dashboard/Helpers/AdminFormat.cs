using System.Globalization;
using DungeonWorldBot.Dashboard.Models.Admin;

namespace DungeonWorldBot.Dashboard.Helpers;

/// <summary>
/// Addresses and stored values as the admin pages show them.
/// </summary>
public static class AdminFormat
{
    private const int MaxCellLength = 60;

    /// <summary>
    /// The database as it goes in the address, "game" or "canvas".
    /// </summary>
    public static string Slug(AdminDatabase database) => database.ToString().ToLowerInvariant();

    public static AdminDatabase? Parse(string? slug) =>
        Enum.TryParse<AdminDatabase>(slug, ignoreCase: true, out var database) && Enum.IsDefined(database) ? database : null;

    public static string Name(AdminDatabase database) => database == AdminDatabase.Canvas ? "Scenes" : "Game";

    public static string TableUrl(AdminDatabase database, string table) => $"admin/{Slug(database)}/{Uri.EscapeDataString(table)}";

    public static string RowUrl(AdminDatabase database, string table, object rowID) =>
        $"{TableUrl(database, table)}/{Convert.ToString(rowID, CultureInfo.InvariantCulture)}";

    /// <summary>
    /// A value as stored, for a form field.
    /// </summary>
    public static string Raw(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// A value for a table cell: cut short when long, files by size.
    /// </summary>
    public static string Cell(object? value) => value switch
    {
        null => "NULL",
        AdminBlob blob => $"‹{blob.Length:N0} bytes›",
        string text when text.Length > MaxCellLength => text[..MaxCellLength].ReplaceLineEndings(" ") + "…",
        string text => text.ReplaceLineEndings(" "),
        _ => Raw(value),
    };
}

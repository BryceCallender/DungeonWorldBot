using System.Globalization;
using DungeonWorldBot.Dashboard.Models.Admin;
using Microsoft.Extensions.Primitives;

namespace DungeonWorldBot.Dashboard.Services.Admin;

/// <summary>
/// Reading the row page's form into what to store. Each column's value is in "v:{column}" (several for flags, added
/// together). Files aren't in the form, so they stay as they are.
/// </summary>
public static class AdminForm
{
    /// <param name="isNew">Whether it's for a row being added, which leaves blank what SQLite fills in itself.</param>
    /// <param name="values">What to store, for the columns the form has and doesn't leave to SQLite.</param>
    /// <returns>Why what was typed won't do, or null when it will.</returns>
    public static string? Read(IReadOnlyList<AdminColumn> columns, IFormCollection form, bool isNew, out IReadOnlyList<AdminValue> values)
    {
        var read = new List<AdminValue>();
        values = read;

        foreach (var column in columns.Where(c => !c.IsBlob))
        {
            if (!form.TryGetValue($"v:{column.Name}", out var typed))
                continue;
            if (Parse(column, typed, isNew, out var value) is { } error)
                return error;
            if (value is not null)
                read.Add(new AdminValue(column.Name, value));
        }

        return null;
    }

    /// <param name="value">What to store, or null to leave it out of a new row for SQLite to fill in.</param>
    private static string? Parse(AdminColumn column, StringValues typed, bool isNew, out object? value)
    {
        value = null;
        if (column.Flags)
            return ParseFlags(column, typed, out value);

        var text = typed.ToString().Replace("\r\n", "\n");
        // A new row leaves blank what SQLite fills in itself, like its ID
        if (isNew && text.Length == 0 && (column.HasDefault || column.PrimaryKey))
            return null;

        // Blank is nothing at all where that's allowed, so "no title" doesn't become a title of ""
        if (column.Nullable && IsBlank(column, text))
        {
            value = DBNull.Value;
            return null;
        }

        if (column.IsInteger)
            return ParseInteger(column, text, out value);
        if (column.IsReal)
            return ParseReal(column, text, out value);

        value = text;
        return null;
    }

    // Spaces are still something to store as text, but not as a number
    private static bool IsBlank(AdminColumn column, string text) =>
        column.IsNumber ? string.IsNullOrWhiteSpace(text) : text.Length == 0;

    private static string? ParseFlags(AdminColumn column, StringValues typed, out object? value)
    {
        value = null;
        long bits = 0;
        foreach (var flag in typed)
        {
            if (!long.TryParse(flag, out var bit))
                return $"{column.Name} takes whole numbers.";
            bits |= bit;
        }

        value = bits;
        return null;
    }

    private static string? ParseInteger(AdminColumn column, string text, out object? value)
    {
        value = null;
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            value = number;
        // Discord IDs past the top of a long are stored wrapped around, as the bot stores them
        else if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var big))
            value = unchecked((long)big);

        return value is null ? $"{column.Name} takes a whole number." : null;
    }

    private static string? ParseReal(AdminColumn column, string text, out object? value)
    {
        value = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;
        return value is null ? $"{column.Name} takes a number." : null;
    }
}

namespace DungeonWorldBot.Dashboard.Models.Admin;

/// <summary>
/// A column of a table, as SQLite has it, with what the bot's model says its numbers mean.
/// </summary>
/// <param name="Type">The declared type, e.g. "INTEGER" or "TEXT".</param>
/// <param name="HasDefault">Whether SQLite fills it in when a new row leaves it out.</param>
/// <param name="Choices">The values it can hold when it's an enum or a yes/no, or empty for anything goes.</param>
/// <param name="Flags">Whether it holds several of its choices at once, added together.</param>
/// <param name="References">The table its values point at, when it's a foreign key to that table's ID.</param>
public record AdminColumn(
    string Name,
    string Type,
    bool NotNull,
    bool PrimaryKey,
    bool HasDefault,
    IReadOnlyList<AdminChoice> Choices,
    bool Flags,
    string? References)
{
    public bool IsInteger => Type.Contains("INT", StringComparison.OrdinalIgnoreCase);

    public bool IsReal => Type.Contains("REAL", StringComparison.OrdinalIgnoreCase)
        || Type.Contains("FLOA", StringComparison.OrdinalIgnoreCase)
        || Type.Contains("DOUB", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pictures and the like, which are shown by size and left as they are.
    /// </summary>
    public bool IsBlob => Type.Contains("BLOB", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// What a stored value means, e.g. "Ranger" for 9, or several joined for flags.
    /// </summary>
    public string? Describe(object? value)
    {
        if (value is null || Choices.Count == 0)
            return null;

        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        if (!Flags)
            return Choices.FirstOrDefault(c => c.Value == text)?.Label;

        if (!long.TryParse(text, out var bits))
            return null;
        var names = Choices.Where(c => long.TryParse(c.Value, out var bit) && bit != 0 && (bits & bit) == bit).Select(c => c.Label).ToList();
        return names.Count == 0 ? Choices.FirstOrDefault(c => c.Value == "0")?.Label : string.Join(", ", names);
    }

    /// <summary>
    /// Whether a flag is set in a stored value.
    /// </summary>
    public bool Has(object? value, AdminChoice choice) =>
        long.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var bits)
        && long.TryParse(choice.Value, out var bit)
        && bit != 0
        && (bits & bit) == bit;
}

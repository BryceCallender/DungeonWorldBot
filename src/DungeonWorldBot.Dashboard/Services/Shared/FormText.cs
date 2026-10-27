namespace DungeonWorldBot.Dashboard.Services.Shared;

/// <summary>
/// Tidying what the DM types into the dashboard's forms before it goes into the bot's database.
/// </summary>
public static class FormText
{
    public const int MaxNameLength = 80;
    public const int MaxTextLength = 1000;

    /// <summary>
    /// A name: trimmed, or null when it's blank or too long to use.
    /// </summary>
    public static string? Name(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxNameLength ? null : trimmed;
    }

    /// <summary>
    /// Optional text: trimmed, null when blank, and cut to length (the form already stops longer).
    /// </summary>
    public static string? Clean(string? value, int maxLength = MaxTextLength)
    {
        // Text boxes send Windows line endings
        var trimmed = value?.Replace("\r\n", "\n").Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    /// <summary>
    /// A number field, which comes through as text, and blank when left empty.
    /// </summary>
    public static int? Number(string? value) => int.TryParse(value, out var number) ? number : null;
}

using System.Text.RegularExpressions;

namespace DungeonWorldBot.Dashboard.Helpers;

/// <summary>
/// Numbers as the pages show them.
/// </summary>
public static class Format
{
    /// <summary>
    /// A modifier with its sign, e.g. "+2" or "-1".
    /// </summary>
    public static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();

    /// <summary>
    /// How many of something, e.g. "1 roll" or "3 rolls".
    /// </summary>
    /// <param name="plural">For words that don't just take an s.</param>
    public static string Plural(int count, string word, string? plural = null) =>
        $"{count} {(count == 1 ? word : plural ?? word + "s")}";

    /// <summary>
    /// A share of a total as whole percent for reading, e.g. "42%".
    /// </summary>
    public static string Percent(int count, int total) => total == 0 ? "0%" : $"{count * 100 / total}%";

    /// <summary>
    /// A fight's roll or result for the dashboard's narrow lists, where the roll's pill colour already says the band:
    /// "9 · Weak Hit (7-9)" becomes "9 · Weak hit", and "💥 4 damage" becomes "4 dmg". Discord keeps the long form.
    /// </summary>
    public static string ShortFightText(string text) =>
        RangeSuffix.Replace(text, "").Replace(" Hit", " hit").Replace("💥 ", "").Replace(" damage", " dmg");

    private static readonly Regex RangeSuffix = new(@"\s*\([^)]*\)$", RegexOptions.Compiled);
}

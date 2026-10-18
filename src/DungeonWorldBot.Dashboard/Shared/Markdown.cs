using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace DungeonWorldBot.Dashboard.Shared;

/// <summary>
/// Shows the bits of Discord markdown the bot writes in move and ability text: **bold**, *italic*, `code` and line breaks.
/// </summary>
public static partial class Markdown
{
    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"\*(.+?)\*")]
    private static partial Regex Italic();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex Code();

    public static MarkupString ToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new MarkupString(string.Empty);

        var html = WebUtility.HtmlEncode(text.Trim());
        html = Bold().Replace(html, "<strong>$1</strong>");
        html = Italic().Replace(html, "<em>$1</em>");
        html = Code().Replace(html, "<code>$1</code>");
        return new MarkupString(html.Replace("\n", "<br>"));
    }

    public static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString();
}

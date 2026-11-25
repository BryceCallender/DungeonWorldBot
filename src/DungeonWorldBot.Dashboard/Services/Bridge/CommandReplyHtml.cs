using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DungeonWorldBot.Features.Bridge;
using DungeonWorldBot.Dashboard.Services.Shared;

namespace DungeonWorldBot.Dashboard.Services.Bridge;

/// <summary>
/// The bot's replies to a command run from the dashboard, drawn like Discord's embeds: a colored edge, the title, the
/// text, then each field.
/// </summary>
public static partial class CommandReplyHtml
{
    // A Discord code block, like the dice the bot shows: ```text``` with an optional language after the first ```
    [GeneratedRegex(@"```(?:[a-z]*\n)?(.*?)```", RegexOptions.Singleline)]
    private static partial Regex CodeBlock();

    public static string For(CommandReply reply)
    {
        var html = new StringBuilder();
        foreach (var message in reply.Messages)
        {
            if (message.Content is { } content)
                html.Append($"<p class=\"reply-content\">{Text(content)}</p>");
            foreach (var embed in message.Embeds)
                Append(html, embed);
        }

        return html.ToString();
    }

    private static void Append(StringBuilder html, ReplyEmbed embed)
    {
        var color = embed.Color is { } rgb ? $" style=\"--embed: #{rgb:X6}\"" : string.Empty;
        html.Append($"<div class=\"reply-embed\"{color}>");
        if (embed.Title is { } title)
            html.Append($"<div class=\"reply-title\">{Text(title)}</div>");
        if (embed.Description is { } description)
            html.Append($"<div class=\"reply-text\">{Text(description)}</div>");
        if (embed.Fields.Count > 0)
        {
            html.Append("<dl class=\"reply-fields\">");
            foreach (var field in embed.Fields)
                html.Append($"<div><dt>{Text(field.Name)}</dt><dd>{Text(field.Value)}</dd></div>");
            html.Append("</dl>");
        }
        if (embed.Footer is { } footer)
            html.Append($"<div class=\"reply-footer\">{WebUtility.HtmlEncode(footer)}</div>");
        html.Append("</div>");
    }

    /// <summary>
    /// The bot's markdown, with its code blocks kept as they're laid out.
    /// </summary>
    private static string Text(string text)
    {
        var html = new StringBuilder();
        var at = 0;
        foreach (Match block in CodeBlock().Matches(text))
        {
            html.Append(Markdown.ToHtml(text[at..block.Index]));
            html.Append($"<pre class=\"reply-code\">{WebUtility.HtmlEncode(block.Groups[1].Value.Trim('\n'))}</pre>");
            at = block.Index + block.Length;
        }

        return html.Append(Markdown.ToHtml(text[at..])).ToString();
    }
}

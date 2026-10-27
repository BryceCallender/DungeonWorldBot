using System.Net;

namespace DungeonWorldBot.Dashboard.Auth;

/// <summary>
/// The card Discord (and other chat apps) show when someone pastes a link to the dashboard.
/// </summary>
/// <remarks>
/// With sign-in on, the bots that fetch links can't sign in, so they'd only ever see the redirect to Discord. They get a
/// bare page with the card instead. It names the site and nothing else, since anyone can pretend to be one of them.
/// </remarks>
public static class LinkPreview
{
    public const string SiteName = "Sana Solem";
    public const string Description = "The party, their fights and their sessions, from the Sana Solem Dungeon World campaign.";
    public const string Image = "banner.webp";
    // The ember orange the site's accent is drawn from, for the stripe down the side of Discord's embed
    public const string ThemeColor = "#c2410c";

    // The link-preview fetchers of the apps the table might paste links into
    private static readonly string[] Fetchers =
    [
        "Discordbot", "Slackbot", "Twitterbot", "facebookexternalhit", "TelegramBot", "WhatsApp", "LinkedInBot",
        "Embedly", "redditbot", "Iframely",
    ];

    public static bool IsFetcher(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) &&
        Fetchers.Any(f => request.Headers.UserAgent.ToString().Contains(f, StringComparison.OrdinalIgnoreCase));

    public static Task WriteCardAsync(HttpContext context)
    {
        var request = context.Request;
        var site = $"{request.Scheme}://{request.Host}{request.PathBase}/";
        var url = WebUtility.HtmlEncode(site + request.Path.Value?.TrimStart('/'));
        var image = WebUtility.HtmlEncode(site + Image);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync($"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <title>{SiteName}</title>
                <meta name="description" content="{Description}" />
                <meta name="theme-color" content="{ThemeColor}" />
                <meta property="og:type" content="website" />
                <meta property="og:site_name" content="{SiteName}" />
                <meta property="og:title" content="{SiteName}" />
                <meta property="og:description" content="{Description}" />
                <meta property="og:url" content="{url}" />
                <meta property="og:image" content="{image}" />
                <meta name="twitter:card" content="summary" />
            </head>
            <body></body>
            </html>
            """, context.RequestAborted);
    }
}

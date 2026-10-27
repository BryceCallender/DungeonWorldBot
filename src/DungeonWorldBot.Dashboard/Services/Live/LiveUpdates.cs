namespace DungeonWorldBot.Dashboard.Services.Live;

/// <summary>
/// A server-sent event stream that tells open pages when the bot changed something, so they can refresh in place.
/// </summary>
public static class LiveUpdates
{
    // Comfortably inside the idle timeouts of tunnels and proxies (Cloudflare's is 100 seconds)
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);

    // A named event, so it doesn't count as a change; the browser drops events with no data
    private const string Ping = "event: ping\ndata: ping\n\n";

    // How long the browser waits to reconnect a dropped stream, sooner than its own default
    private const string Retry = "retry: 2000\n\n";

    public static RouteHandlerBuilder MapLiveUpdates(this IEndpointRouteBuilder app) =>
        app.MapGet("/live", async (HttpContext context, DatabaseWatcher watcher) =>
        {
            var ct = context.RequestAborted;
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            // Stops proxies from holding events back until a buffer fills
            context.Response.Headers["X-Accel-Buffering"] = "no";

            try
            {
                // Pings are events rather than comments so the page can see them and show it's live
                await SendAsync(context, Retry + Ping);

                while (!ct.IsCancellationRequested)
                {
                    var change = watcher.NextChange;
                    var finished = await Task.WhenAny(change, Task.Delay(Heartbeat, ct));
                    await SendAsync(context, finished == change ? "data: changed\n\n" : Ping);
                }
            }
            catch (OperationCanceledException)
            {
                // The page closed
            }
        });

    private static async Task SendAsync(HttpContext context, string text)
    {
        await context.Response.WriteAsync(text, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}

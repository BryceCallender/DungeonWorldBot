namespace DungeonWorldBot.Dashboard.Party;

/// <summary>
/// A server-sent event stream that tells open pages when the bot changed something, so they can refresh in place.
/// </summary>
public static class LiveUpdates
{
    // Comfortably inside the idle timeouts of tunnels and proxies (Cloudflare's is 100 seconds)
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);

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
                await SendAsync(context, ": connected\n\n");

                while (!ct.IsCancellationRequested)
                {
                    var change = watcher.NextChange;
                    var finished = await Task.WhenAny(change, Task.Delay(Heartbeat, ct));
                    await SendAsync(context, finished == change ? "data: changed\n\n" : ": ping\n\n");
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

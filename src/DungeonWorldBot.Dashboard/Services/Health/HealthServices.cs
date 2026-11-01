using DungeonWorldBot.Dashboard.Models.Shared;
using DungeonWorldBot.Infrastructure.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DungeonWorldBot.Dashboard.Services.Health;

public static class HealthServices
{
    /// <summary>
    /// The dashboard's logs, traces and metrics (see <see cref="Telemetry"/>), and the health page that shows how it
    /// and the bot are doing.
    /// </summary>
    public static IServiceCollection AddHealth(this IServiceCollection services, DashboardDatabases databases, IConfiguration configuration) =>
        services
            .AddSanaSolemTelemetry(
                configuration,
                "sana-solem-dashboard",
                tracing => tracing.AddAspNetCoreInstrumentation(options => options.Filter = context => Traced(context.Request.Path)),
                metrics => metrics.AddAspNetCoreInstrumentation())
            .AddScoped(provider => ActivatorUtilities.CreateInstance<HealthReader>(provider, databases));

    // Each page, form post and API call, but not the live stream and canvas hub (held open for hours) or the scripts and pictures
    private static bool Traced(PathString path) =>
        !path.StartsWithSegments("/live")
        && !path.StartsWithSegments("/hubs")
        && !path.StartsWithSegments("/_framework")
        && !Path.HasExtension(path.Value);
}

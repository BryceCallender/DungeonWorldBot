using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DungeonWorldBot.Infrastructure.Telemetry;

/// <summary>
/// Logs, traces and metrics for the bot and the dashboard, sent over OpenTelemetry to whatever collector
/// OTEL_EXPORTER_OTLP_ENDPOINT names, like the Aspire Dashboard on http://localhost:4317. Without it nothing is sent,
/// so the Pi runs as before.
/// </summary>
public static class Telemetry
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// Whether there's somewhere to send telemetry.
    /// </summary>
    public static bool Enabled(IConfiguration configuration) => !string.IsNullOrWhiteSpace(configuration[EndpointKey]);

    /// <param name="serviceName">What it's called in the collector, e.g. "sana-solem-bot".</param>
    /// <param name="tracing">Anything else to trace, like the dashboard's page requests.</param>
    /// <param name="metering">Anything else to measure, like how long the dashboard's pages take.</param>
    public static IServiceCollection AddSanaSolemTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<TracerProviderBuilder>? tracing = null,
        Action<MeterProviderBuilder>? metering = null)
    {
        // Kept whether or not telemetry is sent, for the dashboard's health page
        services.AddSingleton<RecentErrors>();
        services.AddSingleton<ILoggerProvider>(provider => provider.GetRequiredService<RecentErrors>());

        if (!Enabled(configuration))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithLogging(logging => { }, options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            })
            .WithTracing(tracer =>
            {
                tracer
                    .AddSource(BotMetrics.Name)
                    // Every call to Discord's API, with how long it took and what Discord said
                    .AddHttpClientInstrumentation();
                tracing?.Invoke(tracer);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(BotMetrics.Name)
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation();
                metering?.Invoke(metrics);
            })
            .UseOtlpExporter();

        return services;
    }
}

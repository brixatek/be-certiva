using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Certiva.Infrastructure.Observability;

/// <summary>
/// Extension methods for registering OpenTelemetry tracing and Prometheus metrics.
/// </summary>
public static class ObservabilityServiceExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing (HTTP, EF Core, Redis, MassTransit) and
    /// Prometheus metrics for the Certiva API service.
    /// </summary>
    public static IServiceCollection AddCertivaObservability(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource("certiva-api")
                .AddAspNetCoreInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddRedisInstrumentation()
                .AddSource("MassTransit"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddPrometheusExporter());

        return services;
    }

    /// <summary>
    /// Maps the OpenTelemetry Prometheus scraping endpoint at <c>/metrics</c>.
    /// </summary>
    public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder app)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }
}

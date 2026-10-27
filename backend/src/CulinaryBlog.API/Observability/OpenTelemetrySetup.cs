using OpenTelemetry;
using System.Diagnostics;
using CulinaryBlog.Application.Common.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// FR-OBS-003 OpenTelemetry configuration. Registers tracing with ASP.NET Core,
/// outbound HttpClient and EF Core instrumentation. Health-check requests are
/// excluded from tracing to avoid noise from orchestrator probes.
/// </summary>
public static class OpenTelemetrySetup
{
public static IServiceCollection AddObservabilityOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
{
    var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
    var enableMetrics = configuration.GetValue<bool>("OpenTelemetry:ExportMetrics");
    var maxQueueSize = configuration.GetValue<int>("OpenTelemetry:MaxQueueSize");
    if (maxQueueSize <= 0) maxQueueSize = 2048;

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("CulinaryBlog.API"))
        .WithTracing(tracing =>
        {
            tracing.AddProcessor<SensitiveTagProcessor>();

            tracing.AddAspNetCoreInstrumentation(options =>
            {
                options.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/health");

                options.EnrichWithHttpResponse = (activity, httpResponse) =>
                {
                    activity.SetTag("http.response.status_code", httpResponse.StatusCode.ToString());
                };
            });

            tracing.AddEntityFrameworkCoreInstrumentation();

            tracing.AddHttpClientInstrumentation();

            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options =>
                {
                    options.ExportProcessorType = ExportProcessorType.Batch;
                    options.TimeoutMilliseconds = 1000;
                    options.BatchExportProcessorOptions.MaxQueueSize = maxQueueSize;
                });
            }
        })
        .WithMetrics(metrics => { metrics.AddMeter("CulinaryBlog"); services.AddSingleton<RecipeMetrics>(); });

    return services;
}
}

using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using CulinaryBlog.Application.Common.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// FR-OBS-003 OpenTelemetry configuration. Registers tracing with ASP.NET Core,
/// outbound HttpClient and EF Core instrumentation. Health-check requests are
/// excluded from tracing to avoid noise from orchestrator probes.
/// </summary>
public static class OpenTelemetrySetup
{
    public static IServiceCollection AddObservabilityOpenTelemetry(this IServiceCollection services)
    {
        services.AddSingleton<RecipeMetrics>();

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
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter("CulinaryBlog");
            });

        services.Configure<OtlpExporterOptions>(options =>
        {
            options.ExportProcessorType = ExportProcessorType.Batch;
            options.TimeoutMilliseconds = 1000;
        });

        services.Configure<BatchExportActivityProcessorOptions>(options =>
        {
            options.MaxQueueSize = 2048;
        });

        services.AddOptions<BatchExportActivityProcessorOptions>()
            .Configure<IConfiguration>((opts, config) =>
            {
                var maxQueueSize = config.GetValue<int>("OpenTelemetry:MaxQueueSize");
                if (maxQueueSize > 0)
                {
                    opts.MaxQueueSize = maxQueueSize;
                }
            });

        services.AddOptions<OtlpExporterOptions>()
            .Configure<IConfiguration>((opts, config) =>
            {
                var endpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
                if (!string.IsNullOrEmpty(endpoint))
                {
                    try { opts.Endpoint = new Uri(endpoint); } catch { }
                }
                var protocol = config["OTEL_EXPORTER_OTLP_PROTOCOL"];
                if (!string.IsNullOrEmpty(protocol) && protocol.Contains("http/protobuf"))
                {
                    try { opts.Protocol = OtlpExportProtocol.HttpProtobuf; } catch { }
                }
            });

        services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
        {
            IConfiguration? configuration = null;
            try
            {
                configuration = sp.GetService(typeof(IConfiguration)) as IConfiguration;
            }
            catch
            {
                return;
            }

            if (configuration == null)
            {
                return;
            }

            var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            var maxQueueSize = configuration.GetValue<int>("OpenTelemetry:MaxQueueSize");
            if (maxQueueSize <= 0)
            {
                maxQueueSize = 2048;
            }

            try
            {
                var options = sp.GetService<IOptionsMonitor<OtlpExporterOptions>>();
                var exporterOptions = options?.Get("default") ?? new OtlpExporterOptions();
                exporterOptions.Endpoint = new Uri(endpoint);
                exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                exporterOptions.TimeoutMilliseconds = 1000;
                var processorOptions = new BatchExportActivityProcessorOptions { MaxQueueSize = maxQueueSize };
                var exporter = new OtlpTraceExporter(exporterOptions);
                var processor = new BatchActivityExportProcessor(exporter, processorOptions.MaxExportBatchSize, processorOptions.ScheduledDelayMilliseconds, processorOptions.ExporterTimeoutMilliseconds, processorOptions.MaxQueueSize);
                tracing.AddProcessor(processor);
            }
            catch
            {
                return;
            }
        });

        services.ConfigureOpenTelemetryMeterProvider((sp, metrics) =>
        {
            IConfiguration? configuration = null;
            try
            {
                configuration = sp.GetService(typeof(IConfiguration)) as IConfiguration;
            }
            catch
            {
                return;
            }

            if (configuration == null)
            {
                return;
            }

            var endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            var enableMetrics = configuration.GetValue<bool>("OpenTelemetry:ExportMetrics");
            if (string.IsNullOrWhiteSpace(endpoint) || !enableMetrics)
            {
                return;
            }

            try
            {
                var options = sp.GetService<IOptionsMonitor<OtlpExporterOptions>>();
                var exporterOptions = options?.Get("default") ?? new OtlpExporterOptions();
                exporterOptions.Endpoint = new Uri(endpoint);
                exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
                var exporter = new OtlpMetricExporter(exporterOptions);
                var readerOptions = new MetricReaderOptions { TemporalityPreference = MetricReaderTemporalityPreference.Delta };
                var reader = new PeriodicExportingMetricReader(exporter);
                metrics.AddReader(reader);
            }
            catch
            {
                return;
            }
        });

        return services;
    }
}

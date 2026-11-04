using CulinaryBlog.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// FR-OBS-001 health-check registration. The probes themselves live in
/// Infrastructure; this file only wires them into the health-check pipeline, so
/// the API project stays the single place that knows about HTTP health routes.
///
/// Three checks are registered under the fixed names <c>postgres</c>,
/// <c>redis</c> and <c>minio</c> (those names are part of the response contract,
/// see the SRS API table rows 31-33), each with the same built-in timeout from
/// <c>HealthChecks:TimeoutSeconds</c>.
///
/// Tags decide which route reports a check:
/// <list type="bullet">
/// <item><c>ready</c> - the dependency is required for readiness. PostgreSQL and
/// Redis carry it; MinIO deliberately does not (FR-OBS-001).</item>
/// <item><c>aggregate</c> - reported by the aggregate <c>/health</c> route. All
/// three carry it.</item>
/// </list>
///
/// The timeout is handed to the health-check framework rather than enforced
/// here: the framework runs each check with that budget and reports Unhealthy on
/// expiry, which is also the bound that lets a probe ignoring its cancellation
/// token be abandoned.
/// </summary>
public static class HealthChecksSetup
{
    /// <summary>Name of the PostgreSQL check, as it appears in the JSON body.</summary>
    public const string PostgresCheckName = "postgres";

    /// <summary>Name of the Redis check, as it appears in the JSON body.</summary>
    public const string RedisCheckName = "redis";

    /// <summary>Name of the MinIO check, as it appears in the JSON body.</summary>
    public const string MinioCheckName = "minio";

    /// <summary>Tag for checks a readiness probe must consider.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Tag for checks the aggregate <c>/health</c> route reports.</summary>
    public const string AggregateTag = "aggregate";

    /// <summary>
    /// Used when <c>HealthChecks:TimeoutSeconds</c> is absent or not an integer.
    /// Two seconds keeps the readiness answer well inside the 10-second probe
    /// interval NFR-REL-001 expects, even when several checks time out at once.
    /// </summary>
    private const int DefaultTimeoutSeconds = 2;

    /// <summary>
    /// Registers the three dependency probes and their health checks. Call this
    /// once, next to <c>AddInfrastructure</c>, so the health routes mapped later
    /// have checks to report.
    /// </summary>
    public static IServiceCollection AddObservabilityHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Transient: each probe runs inside the scope the health-check service
        // creates per execution, so a scoped dependency (ApplicationDbContext)
        // resolves correctly and no probe instance is shared across requests.
        services.AddTransient<IPostgresProbe, PostgresProbe>();
        services.AddTransient<IRedisProbe, RedisProbe>();
        services.AddTransient<IMinioProbe, MinioProbe>();

        var timeoutSeconds =
            configuration.GetValue<int?>("HealthChecks:TimeoutSeconds") ?? DefaultTimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Every dependency reports Unhealthy on failure, never Degraded: the
        // response contract has only Healthy and Unhealthy, and a Degraded
        // result would otherwise be mapped to 503 while printing "Healthy".
        //
        // The type argument is the registered INTERFACE, not the concrete probe.
        // AddCheck<T> builds T through ActivatorUtilities, which resolves each
        // constructor parameter from the container and does NOT fall back to
        // constructing an unregistered concrete type - passing PostgresProbe here
        // threw "Unable to resolve service for type ...PostgresProbe" the first
        // time a check ran. The interface is also what tests must be able to
        // substitute, so the fake probe a test registers is the one that runs.
        services.AddHealthChecks()
            .AddCheck<ProbeHealthCheck<IPostgresProbe>>(
                PostgresCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { ReadyTag, AggregateTag },
                timeout: timeout)
            .AddCheck<ProbeHealthCheck<IRedisProbe>>(
                RedisCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { ReadyTag, AggregateTag },
                timeout: timeout)
            .AddCheck<ProbeHealthCheck<IMinioProbe>>(
                MinioCheckName,
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { AggregateTag },
                timeout: timeout);

        return services;
    }

    public static HealthCheckOptions CreateOptions(string tag, bool json)
    {
        return new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(tag),
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            AllowCachingResponses = false,
            ResponseWriter = async (context, report) =>
            {
                var status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";
                context.Response.ContentType = json ? "application/json" : "text/plain";

                if (!json)
                {
                    await context.Response.WriteAsync(status, context.RequestAborted);
                    return;
                }

                var entries = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new Dictionary<string, string>
                    {
                        ["status"] = entry.Value.Status == HealthStatus.Healthy
                            ? "Healthy"
                            : "Unhealthy"
                    });
                var payload = new Dictionary<string, object>
                {
                    ["status"] = status,
                    ["entries"] = entries
                };

                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    payload,
                    cancellationToken: context.RequestAborted);
            }
        };
    }
}
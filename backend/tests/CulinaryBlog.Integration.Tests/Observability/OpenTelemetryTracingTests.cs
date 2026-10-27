using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

[Collection("OpenTelemetryTracing")]
public sealed class OpenTelemetryTracingTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private List<Activity> _activities = null!;
    private CollectingLogSink _sink = null!;

    static OpenTelemetryTracingTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test");
    }

    public Task InitializeAsync()
    {
        _activities = new List<Activity>();
        _sink = new CollectingLogSink();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                ["Redis:ConnectionString"] = ""
            }));
            b.ConfigureServices(services =>
            {
                services.RemoveAll<Microsoft.EntityFrameworkCore.DbContextOptions<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>>();
                services.RemoveAll<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>();
                services.AddDbContext<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase("OT01Db"));

                services.ConfigureOpenTelemetryTracerProvider(tp => tp.AddProcessor(new CollectingActivityProcessor(_activities)));

                services.RemoveAll<ILogEventSink>();
                services.AddSingleton<ILogEventSink>(_sink);
            });
        });

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Tracing_PublicEndpoint_RecordsActivityWithMethodAndStatus()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/?q=SENTINEL-QUERY-123");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(3000);

        var serverActivities = _activities.Where(a => a.Kind == ActivityKind.Server).ToList();
        // Assert exactly one server activity
        Assert.Single(serverActivities);

        var activity = serverActivities.First();

        // Observed tag name: http.request.method = GET (confirmed by observation)
        // http.response.status_code is NOT present as a tag in OT 1.20.0 ASP.NET Core instrumentation
        var methodTag = activity.Tags.FirstOrDefault(t => t.Key == "http.request.method");
        Assert.NotNull(methodTag.Value);
        Assert.Equal("GET", methodTag.Value?.ToString());

        // http.response.status_code added via EnrichWithHttpResponse callback in OpenTelemetrySetup
        var statusTag = activity.Tags.FirstOrDefault(t => t.Key == "http.response.status_code");
        Assert.NotNull(statusTag.Value);
        Assert.Equal("200", statusTag.Value?.ToString());

        // Verify no tag value contains sentinel (all activities)
        foreach (var act in _activities)
        {
            foreach (var tag in act.Tags)
            {
                if (tag.Value != null)
                {
                    Assert.DoesNotContain("SENTINEL-QUERY-123", tag.Value.ToString()!);
                }
            }
            foreach (var evt in act.Events)
            {
                foreach (var attr in evt.Tags)
                {
                    if (attr.Value != null)
                    {
                        Assert.DoesNotContain("SENTINEL-QUERY-123", attr.Value.ToString()!);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task Tracing_HealthLive_ProducesNoRecordedActivity()
    {
        _activities.Clear();
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(3000);

        // Check ALL server activities recorded by collector (do not filter by path tag)
        var allServerActivities = _activities.Where(a => a.Kind == ActivityKind.Server).ToList();

        // If any activity exists, assert IsAllDataRequested == false for each
        foreach (var act in allServerActivities)
        {
            Assert.False(act.IsAllDataRequested,
                $"Health activity should not be fully recorded. Activity: {act.DisplayName}");
        }

        // Assert no server activity was recorded for /health/live
        // Check both path tag and DisplayName for "health"
        var healthActivities = allServerActivities.Where(a =>
        {
            var pathTag = a.Tags.FirstOrDefault(t => t.Key == "url.path" || t.Key == "http.route");
            var path = pathTag.Value?.ToString() ?? a.DisplayName;
            return path.Contains("health", StringComparison.OrdinalIgnoreCase);
        }).ToList();
        Assert.Empty(healthActivities);
    }

    [Fact]
    public async Task Tracing_LogEvent_TraceIdMatchesActivity()
    {
        _activities.Clear();
        _sink.Clear();
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/?q=trace-test");
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(3000);

        var serverActivity = _activities.FirstOrDefault(a => a.Kind == ActivityKind.Server);
        Assert.NotNull(serverActivity);

        var logEvents = _sink.Events;
        Assert.NotEmpty(logEvents);

        // Verify top-level TraceId/SpanId of log event match activity
        var matchingLog = logEvents.FirstOrDefault(e =>
            e.TraceId == serverActivity.TraceId);
        Assert.NotNull(matchingLog);
        Assert.Equal(serverActivity.TraceId, matchingLog.TraceId);
        Assert.Equal(serverActivity.SpanId, matchingLog.SpanId);

        // CorrelationId part deferred (S-08 WAITING)
    }
}
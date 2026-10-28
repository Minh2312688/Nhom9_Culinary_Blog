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

    private static HttpRequestMessage CreateTracedRequest(HttpMethod method, string uri, out ActivityTraceId traceId)
    {
        traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            $"00-{traceId.ToHexString()}-{parentSpanId.ToHexString()}-01");
        return request;
    }

    [Fact]
    public async Task Tracing_PublicEndpoint_RecordsActivityWithMethodAndStatus()
    {
        using var client = _factory.CreateClient();
        using var request = CreateTracedRequest(HttpMethod.Get, "/?q=SENTINEL-QUERY-123", out var traceId);
        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(3000);

        var serverActivities = _activities
            .Where(a => a.Kind == ActivityKind.Server && a.TraceId == traceId)
            .ToList();
        Assert.Single(serverActivities);

        var activity = serverActivities.First();

        var methodTag = activity.Tags.FirstOrDefault(t => t.Key == "http.request.method");
        Assert.NotNull(methodTag.Value);
        Assert.Equal("GET", methodTag.Value?.ToString());

        var statusTag = activity.Tags.FirstOrDefault(t => t.Key == "http.response.status_code");
        Assert.NotNull(statusTag.Value);
        Assert.Equal("200", statusTag.Value?.ToString());

        foreach (var act in _activities.Where(a => a.TraceId == traceId))
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
        using var request = CreateTracedRequest(HttpMethod.Get, "/health/live", out var traceId);
        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(3000);

        var healthActivities = _activities
            .Where(a => a.Kind == ActivityKind.Server && a.TraceId == traceId)
            .ToList();

        foreach (var act in healthActivities)
        {
            Assert.False(act.IsAllDataRequested,
                $"Health activity should not be fully recorded. Activity: {act.DisplayName}");
        }

        Assert.Empty(healthActivities);
    }

    [Fact]
    public async Task Tracing_LogEvent_TraceIdMatchesActivity()
    {
        _activities.Clear();
        _sink.Clear();
        using var client = _factory.CreateClient();
        using var request = CreateTracedRequest(HttpMethod.Get, "/?q=trace-test", out var traceId);
        var response = await client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(3000);

        var serverActivity = _activities.FirstOrDefault(
            a => a.Kind == ActivityKind.Server && a.TraceId == traceId);
        Assert.NotNull(serverActivity);

        var matchingLog = _sink.Events.FirstOrDefault(e => e.TraceId == traceId);
        Assert.NotNull(matchingLog);
        Assert.Equal(serverActivity.TraceId, matchingLog.TraceId);
        Assert.Equal(serverActivity.SpanId, matchingLog.SpanId);

    }
}
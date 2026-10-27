using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog.Events;
using System.Collections.Generic;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

[Collection("SerilogSinks")]
public class SerilogMinimumLevelTests : IDisposable
{
    private const string HealthPath = "/health/live";

    public void Dispose() { }

    private static async Task<LogEvent?> FindRequestSummaryEventAsync(SerilogWebApplicationFactory factory, string path, int seconds = 5)
    {
        for (var attempt = 0; attempt < seconds * 10; attempt++)
        {
            var logEvent = factory.Sink.Events.SingleOrDefault(logEvent =>
                logEvent.Properties.TryGetValue("RequestPath", out var rp) && rp.ToString().Trim('"') == path &&
                logEvent.Properties.ContainsKey("RequestMethod") &&
                logEvent.Properties.ContainsKey("StatusCode") &&
                logEvent.Properties.ContainsKey("Elapsed"));

            if (logEvent != null) return logEvent;
            await Task.Delay(100);
        }
        return null;
    }

    [Fact]
    public async Task DefaultConfig_HealthRequest_NoRequestSummaryEvent()
    {
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();
        using var client = factory.CreateClient();
        var response = await client.GetAsync(HealthPath);
        response.EnsureSuccessStatusCode();
        await Task.Delay(500);
        var evt = factory.Sink.Events.SingleOrDefault(e =>
            e.Properties.TryGetValue("RequestPath", out var rp) && rp.ToString().Trim('"') == HealthPath &&
            e.Properties.ContainsKey("RequestMethod") && e.Properties.ContainsKey("StatusCode") && e.Properties.ContainsKey("Elapsed"));
        Assert.Null(evt);
    }

    [Fact]
    public async Task DebugConfig_HealthRequest_OneDebugRequestSummaryEvent()
    {
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:MinimumLevel"] = "Debug"
        })));
        using var client = host.CreateClient();
        var response = await client.GetAsync(HealthPath);
        response.EnsureSuccessStatusCode();
        var evt = await FindRequestSummaryEventAsync(factory, HealthPath, 10);
        Assert.NotNull(evt);
        Assert.Equal(LogEventLevel.Debug, evt.Level);
        var sourceContext = evt.Properties.TryGetValue("SourceContext", out var sc) ? sc.ToString().Trim('"') : null;
        Assert.Equal("Serilog.AspNetCore.RequestLoggingMiddleware", sourceContext);
    }

    [Fact]
    public async Task DefaultConfig_NoMicrosoftAspNetCoreEvents()
    {
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();
        using var client = factory.CreateClient();
        var response = await client.GetAsync(HealthPath);
        response.EnsureSuccessStatusCode();
        await Task.Delay(500);
        Assert.DoesNotContain(factory.Sink.Events, e =>
        {
            if (e.Properties.TryGetValue("SourceContext", out var sc))
            {
                return sc.ToString().Trim('"').StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal);
            }
            return false;
        });
    }

    [Fact]
    public async Task DefaultConfig_NormalRequest_OneInformationSummaryEvent()
    {
        using var factory = new SerilogWebApplicationFactory();
        factory.Sink.Clear();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/categories");
        await Task.Delay(500);
        var matching = factory.Sink.Events.Where(e =>
            e.Properties.TryGetValue("RequestPath", out var rp) && rp.ToString().Trim('"') == "/api/v1/categories" &&
            e.Properties.ContainsKey("RequestMethod") && e.Properties.ContainsKey("StatusCode") && e.Properties.ContainsKey("Elapsed")).ToList();
        Assert.Single(matching);
        Assert.Equal(LogEventLevel.Information, matching[0].Level);
    }
}

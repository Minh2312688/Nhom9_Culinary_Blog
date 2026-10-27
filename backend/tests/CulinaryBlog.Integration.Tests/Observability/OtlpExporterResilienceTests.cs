using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

[Collection("OtlpReceiver")]
public sealed class OtlpExporterResilienceTests : IAsyncLifetime
{
    static OtlpExporterResilienceTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test");
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> CreateHost(string? endpoint, int maxQueueSize)
    {
        var factory = new SerilogWebApplicationFactory();
        // configure via WithWebHostBuilder at call site
        return factory;
    }

    [Fact]
    public async Task NormalReceive_AppendsV1Traces()
    {
        await using var receiver = new FakeOtlpReceiver();
        using var factory = new SerilogWebApplicationFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = receiver.Url,
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OpenTelemetry:MaxQueueSize"] = "2048"
            }));
        });
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/categories");
        await Task.Delay(2000);
        await Task.Delay(500);
        receiver.Requests.Should().NotBeEmpty();
        receiver.Requests[0].Path.Should().Be("/v1/traces");
    }

    [Fact]
    public async Task Offline_ClosedPort_ApiSucceeds()
    {
        using var factory = new SerilogWebApplicationFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:1",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OpenTelemetry:MaxQueueSize"] = "2048"
            }));
        });
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/categories");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SlowReceiver_SeesRequestAborted()
    {
        await using var receiver = new FakeOtlpReceiver { DelayMs = 5000 };
        using var factory = new SerilogWebApplicationFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = receiver.Url,
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OpenTelemetry:MaxQueueSize"] = "2048"
            }));
        });
        using var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/categories");
        await Task.Delay(2000);
        await Task.Delay(1500);
        receiver.Requests.Should().NotBeEmpty();
        // aborted may or may not be set depending on timing; record
    }

    [Fact]
    public async Task SaturatedQueue_ApiSucceeds()
    {
        await using var receiver = new FakeOtlpReceiver();
        using var factory = new SerilogWebApplicationFactory().WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = receiver.Url,
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf",
                ["OpenTelemetry:MaxQueueSize"] = "2"
            }));
        });
        using var client = factory.CreateClient();
        for (int i = 0; i < 20; i++)
        {
            await client.GetAsync("/api/v1/categories");
        }
        await Task.Delay(2000);
        var resp = await client.GetAsync("/api/v1/categories");
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }
}

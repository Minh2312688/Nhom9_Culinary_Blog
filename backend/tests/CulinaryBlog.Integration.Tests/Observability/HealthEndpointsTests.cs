using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Readiness_ShouldRequirePostgresAndRedisButNotMinio()
    {
        using var factory = new ObservabilityWebApplicationFactory();
        using var client = factory.CreateClient();

        var healthy = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);

        factory.PostgresProbe.IsHealthy = false;
        var postgresDown = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, postgresDown.StatusCode);

        factory.PostgresProbe.IsHealthy = true;
        factory.RedisProbe.IsHealthy = false;
        var redisDown = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, redisDown.StatusCode);

        factory.PostgresProbe.IsHealthy = false;
        var bothDown = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, bothDown.StatusCode);

        factory.PostgresProbe.IsHealthy = true;
        factory.RedisProbe.IsHealthy = true;
        factory.MinioProbe.IsHealthy = false;
        var minioDown = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, minioDown.StatusCode);
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("redis")]
    [InlineData("minio")]
    public async Task Aggregate_ShouldReturnMinimalJsonAndReportEachDependencyFailure(string failedProbe)
    {
        using var factory = new ObservabilityWebApplicationFactory();
        using var client = factory.CreateClient();
        GetProbe(factory, failedProbe).IsHealthy = false;

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());

        var entries = root.GetProperty("entries");
        Assert.Equal(new[] { "minio", "postgres", "redis" }, entries.EnumerateObject()
            .Select(entry => entry.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Unhealthy", entries.GetProperty(failedProbe).GetProperty("status").GetString());
        Assert.All(entries.EnumerateObject(), entry =>
        {
            Assert.Equal(new[] { "status" }, entry.Value.EnumerateObject()
                .Select(property => property.Name));
            Assert.Equal(
                entry.Name == failedProbe ? "Unhealthy" : "Healthy",
                entry.Value.GetProperty("status").GetString());
        });
    }

    [Fact]
    public async Task HealthRoutes_ShouldBeAnonymousUncachedAndExposeOnlyContractStatuses()
    {
        using var factory = new ObservabilityWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.PostgresProbe.ExceptionToThrow = new InvalidOperationException("HEALTH-SENTINEL-SECRET");

        foreach (var route in new[] { "/health/live", "/health/ready", "/health" })
        {
            using var response = await client.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal("no-store, no-cache", response.Headers.CacheControl?.ToString());
            Assert.Contains("no-cache", response.Headers.Pragma.ToString());
            Assert.DoesNotContain("HEALTH-SENTINEL-SECRET", body, StringComparison.Ordinal);

            if (route == "/health")
            {
                Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
                using var json = JsonDocument.Parse(body);
                Assert.Equal(new[] { "entries", "status" }, json.RootElement.EnumerateObject()
                    .Select(property => property.Name).Order(StringComparer.Ordinal));
                Assert.Equal(new[] { "minio", "postgres", "redis" }, json.RootElement.GetProperty("entries")
                    .EnumerateObject().Select(entry => entry.Name).Order(StringComparer.Ordinal));
            }
            else
            {
                Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
                Assert.True(body is "Healthy" or "Unhealthy");
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadinessTimeout_ShouldReturn503AndAllowNextRequest(bool observeCancellation)
    {
        using var factory = new ObservabilityWebApplicationFactory
        {
            HealthCheckTimeoutSeconds = 1
        };
        using var client = factory.CreateClient();
        factory.PostgresProbe.Delay = observeCancellation
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromSeconds(5);
        factory.PostgresProbe.ObserveCancellation = observeCancellation;
        factory.PostgresProbe.DelayFirstCallOnly = true;

        var stopwatch = Stopwatch.StartNew();
        var timedOut = await client.GetAsync("/health/ready");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, timedOut.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Timed out after {stopwatch.Elapsed}.");

        var nextRequest = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, nextRequest.StatusCode);
    }

    private static ControllableHealthProbe GetProbe(
        ObservabilityWebApplicationFactory factory,
        string probeName) =>
        probeName switch
        {
            "postgres" => factory.PostgresProbe,
            "redis" => factory.RedisProbe,
            "minio" => factory.MinioProbe,
            _ => throw new ArgumentOutOfRangeException(nameof(probeName), probeName, null)
        };
}

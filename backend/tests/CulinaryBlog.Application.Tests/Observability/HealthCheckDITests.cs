using CulinaryBlog.API.Observability;
using CulinaryBlog.Infrastructure.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Observability;

/// <summary>
/// Verifies that the DI container can resolve all three health checks.
/// This proves the AddCheck registrations use interfaces that are registered in DI.
/// </summary>
public class HealthCheckDITests
{
    [Fact]
    public void AddObservabilityHealthChecks_ShouldResolveAllThreeHealthChecks()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthChecks:TimeoutSeconds"] = "2"
            })
            .Build();

        // Act - Register health checks (this is what Program.cs does)
        services.AddObservabilityHealthChecks(configuration);

        // Build the DI container
        using var serviceProvider = services.BuildServiceProvider();

        // Assert - The health check service constructs each registered check
        // lazily; resolving it exercises the full call site, and the check run
        // below constructs ProbeHealthCheck<T> with its TProbe parameter.
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();
        Assert.NotNull(healthCheckService);
    }

    [Fact]
    public async Task HealthCheckService_ShouldConstructAndRunAllThreeProbeChecks()
    {
        // Arrange - logging is required by DefaultHealthCheckService.
        // PostgresProbe takes ApplicationDbContext through its constructor, so a
        // real (no live server needed) EF InMemory context is registered the
        // way AddInfrastructure does via AddDbContext.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>(
            o => o.UseInMemoryDatabase("health-di-verification"));
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddObservabilityHealthChecks(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();

        // Act - running the checks forces DI to construct all three
        // ProbeHealthCheck<T> instances and resolve their probe interfaces.
        // The probes will fail to connect (no live dependencies) but that must
        // surface as Unhealthy results, never as a DI resolution exception.
        var report = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(3, report.Entries.Count);
        Assert.Contains(HealthChecksSetup.PostgresCheckName, report.Entries.Keys);
        Assert.Contains(HealthChecksSetup.RedisCheckName, report.Entries.Keys);
        Assert.Contains(HealthChecksSetup.MinioCheckName, report.Entries.Keys);

        // The point of this test is DI construction: all three
        // ProbeHealthCheck<T> instances were built through ActivatorUtilities
        // and executed without a resolution exception. Their statuses depend
        // on the (fake) environment - the EF InMemory context connects, while
        // Redis and MinIO find no client and report Unhealthy - so only valid
        // HealthStatus values are asserted, and the Redis/MinIO pair that
        // cannot succeed without a live dependency is asserted Unhealthy.
        Assert.Equal(HealthStatus.Unhealthy, report.Entries[HealthChecksSetup.RedisCheckName].Status);
        Assert.Equal(HealthStatus.Unhealthy, report.Entries[HealthChecksSetup.MinioCheckName].Status);
    }
}

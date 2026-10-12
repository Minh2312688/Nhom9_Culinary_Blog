using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Sitemap;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Sitemap;

/// <summary>
/// FR-JOB-003 wiring verification (SRS-AUDIT §3.6 / NFR-SEO-003).
///
/// The Sitemap generator, ping client, job, options, and recurring scheduler
/// are only "integrated" when the production composition root actually
/// registers them. These tests inspect the service descriptors produced by
/// <see cref="DependencyInjection.AddInfrastructure"/> so a removed or
/// misplaced registration fails fast. They never build/start the Hangfire
/// server, never open a PostgreSQL/MinIO connection, and never send a ping:
/// descriptor inspection is side-effect free and safe for shared CI.
///
/// The scheduler must be absent in the "Testing" environment (matching the
/// existing Hangfire/enqueuer gating) so integration tests never arm a real
/// recurring job.
/// </summary>
public sealed class SitemapDependencyRegistrationTests
{
    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] =
                    "Host=localhost;Port=5432;Database=wiring_test;Username=test;Password=test",
                ["Jwt:Key"] = "WiringTestSecretKey_MustBeAtLeast256BitsLong_ForHmacSha256!",
                ["MinIO:Endpoint"] = "localhost:9000",
                ["MinIO:AccessKey"] = "test",
                ["MinIO:SecretKey"] = "test",
                // A valid production-shaped base URL; empty values are rejected at run time.
                ["Sitemap:PublicBaseUrl"] = "https://food.example.test"
            })
            .Build();

    private static IHostEnvironment CreateEnvironment(string name) => new StubHostEnvironment(name);

    [Fact]
    public void AddInfrastructure_ProductionEnvironment_RegistersSitemapJobAndDependencies()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfrastructure(BuildConfiguration(), CreateEnvironment("Production"));

        // The job itself (scoped factory) is resolvable.
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(SitemapJob));

        // Collaborators the job needs are registered.
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(SitemapOptions));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(SitemapXmlGenerator));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(GoogleSitemapPingClient));
    }

    [Fact]
    public void AddInfrastructure_ProductionEnvironment_RegistersRecurringSchedulerHostedService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfrastructure(BuildConfiguration(), CreateEnvironment("Production"));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(SitemapScheduleHostedService));
    }

    [Fact]
    public void AddInfrastructure_TestingEnvironment_DoesNotRegisterSitemapScheduler()
    {
        // Integration tests run in "Testing"; the live recurring job must not be
        // armed there (same gating as Hangfire storage/server and enqueuers).
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfrastructure(BuildConfiguration(), CreateEnvironment("Testing"));

        services.Should().NotContain(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(SitemapScheduleHostedService));
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(SitemapJob));
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "CulinaryBlog.WiringTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

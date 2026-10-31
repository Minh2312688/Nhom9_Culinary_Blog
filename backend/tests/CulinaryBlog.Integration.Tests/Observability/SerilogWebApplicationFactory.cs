using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// Factory for the Part 2 Serilog tests. Mirrors ObservabilityWebApplicationFactory
/// (environment Testing, in-memory database) but registers a CollectingLogSink so
/// ReadFrom.Services picks it up, and exposes optional overrides for the
/// Serilog:FilePath and Seq configuration keys. Kept separate from the health
/// factory so the Serilog tests never influence the staged health test files.
/// </summary>
public class SerilogWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"SerilogTestDb_{Guid.NewGuid():N}";

    static SerilogWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres",
            "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test");
    }

    /// <summary>Sink collecting every event whose pipeline configuration ran ReadFrom.Services.</summary>
    public CollectingLogSink Sink { get; } = new();

    public string? SerilogFilePath { get; set; }

    public string? SeqServerUrl { get; set; }

    public string? SeqApiKey { get; set; }

    /// <summary>When false, the collecting sink is not registered; Console tests use this.</summary>
    public bool RegisterSink { get; set; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] =
                    "Host=localhost;Port=5432;Database=serilog_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!"
            };

            if (SerilogFilePath is not null)
            {
                overrides["Serilog:FilePath"] = SerilogFilePath;
            }

            if (SeqServerUrl is not null)
            {
                overrides["Seq:ServerUrl"] = SeqServerUrl;
            }

            if (SeqApiKey is not null)
            {
                overrides["Seq:ApiKey"] = SeqApiKey;
            }

            configuration.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            if (RegisterSink)
            {
                services.RemoveAll<ILogEventSink>();
                services.AddSingleton<ILogEventSink>(Sink);
            }
        });
    }
}

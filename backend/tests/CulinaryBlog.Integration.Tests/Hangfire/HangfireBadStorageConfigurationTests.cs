using System.Diagnostics;
using CulinaryBlog.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Hangfire;

/// <summary>
/// FR-JOB-001 storage misconfiguration guard: when the Hangfire/PostgreSQL
/// connection string points at an unreachable server, starting the real host
/// built through the production AddInfrastructure wiring must fail fast with a
/// clear error that names the endpoint. The error must NOT be swallowed and
/// the host must NOT report a successful start (no degraded mode - guarded by
/// StartupConnectionMaxRetries=0 and AllowDegradedModeWithoutStorage=false in
/// DependencyInjection). Uses only 127.0.0.1:1 (immediately refused); it
/// never touches a shared database.
/// </summary>
public sealed class HangfireBadStorageConfigurationTests
{
    [Fact]
    public async Task AddInfrastructure_WithUnreachableHangfireStorage_FailsFastAtHostStart_WithClearError()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] =
                    "Host=127.0.0.1;Port=1;Database=hangfire_bad_storage;Username=hangfire_bad;Password=hangfire_bad",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                // Keep unrelated integrations out of the picture: memory cache, no MinIO.
                ["Redis:ConnectionString"] = "",
                ["MinIO:Endpoint"] = "",
            })
            .Build();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // environment: null -> not "Testing" -> Hangfire storage/server registered.
                services.AddInfrastructure(configuration, environment: null);
            })
            .Build();

        var stopwatch = Stopwatch.StartNew();
        Exception? thrown = null;
        try
        {
            await host.StartAsync();
        }
        catch (Exception ex)
        {
            thrown = ex;
        }
        stopwatch.Stop();

        if (thrown is null)
        {
            await host.StopAsync(TimeSpan.FromSeconds(5));
        }

        thrown.Should().NotBeNull(
            "an unreachable Hangfire storage must abort host startup instead of silently starting a non-functional job server");

        var chain = Flatten(thrown!).ToList();
        chain.Should().Contain(e => e.GetType().Name.Contains("Npgsql", StringComparison.Ordinal),
            "the storage driver failure must surface to the caller");
        chain.Select(e => e.Message).Should().Contain(m => m.Contains("127.0.0.1:1", StringComparison.Ordinal),
            "the error must name the misconfigured endpoint so it is actionable");

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "StartupConnectionMaxRetries=0 must fail fast instead of retrying silently for a long time");
    }

    private static IEnumerable<Exception> Flatten(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }
    }
}

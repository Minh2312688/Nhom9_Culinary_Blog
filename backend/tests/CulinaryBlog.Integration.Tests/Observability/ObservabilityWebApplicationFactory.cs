using CulinaryBlog.Infrastructure.Health;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CulinaryBlog.Integration.Tests.Observability;

public sealed class ObservabilityWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"ObservabilityTestDb_{Guid.NewGuid():N}";

    static ObservabilityWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable(
            "Jwt__Key",
            "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__Postgres",
            "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test");
    }

    public int HealthCheckTimeoutSeconds { get; set; } = 2;

    public ControllableHealthProbe PostgresProbe { get; } = new();
    public ControllableHealthProbe RedisProbe { get; } = new();
    public ControllableHealthProbe MinioProbe { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] =
                    "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test",
                ["Jwt:Key"] = "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!",
                ["HealthChecks:TimeoutSeconds"] = HealthCheckTimeoutSeconds.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));

            services.RemoveAll<IPostgresProbe>();
            services.RemoveAll<IRedisProbe>();
            services.RemoveAll<IMinioProbe>();
            services.AddSingleton<IPostgresProbe>(PostgresProbe);
            services.AddSingleton<IRedisProbe>(RedisProbe);
            services.AddSingleton<IMinioProbe>(MinioProbe);
        });
    }

    public void ResetProbeCalls()
    {
        PostgresProbe.Reset();
        RedisProbe.Reset();
        MinioProbe.Reset();
    }
}

public sealed class ControllableHealthProbe :
    IPostgresProbe,
    IRedisProbe,
    IMinioProbe
{
    private int callCount;

    public bool IsHealthy { get; set; } = true;

    public Exception? ExceptionToThrow { get; set; }

    public TimeSpan Delay { get; set; }

    public bool ObserveCancellation { get; set; }

    public bool DelayFirstCallOnly { get; set; }

    public int CallCount => Volatile.Read(ref callCount);

    public async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var currentCall = Interlocked.Increment(ref callCount);

        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        if ((!DelayFirstCallOnly || currentCall == 1) &&
            ObserveCancellation &&
            Delay == Timeout.InfiniteTimeSpan)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        else if (Delay > TimeSpan.Zero && (!DelayFirstCallOnly || currentCall == 1))
        {
            await Task.Delay(Delay, ObserveCancellation ? ct : CancellationToken.None);
        }

        return IsHealthy;
    }

    public void Reset()
    {
        Interlocked.Exchange(ref callCount, 0);
    }
}

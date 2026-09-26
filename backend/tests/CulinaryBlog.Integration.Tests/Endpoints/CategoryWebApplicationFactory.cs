using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CulinaryBlog.Integration.Tests.Endpoints;

/// <summary>
/// Factory cho API test Category: thay ApplicationDbContext bằng EF InMemory và thay
/// ICategoryCache bằng cache in-memory, nên test không cần PostgreSQL/Redis thật.
/// </summary>
public sealed class CategoryWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"CategoryApiTestDb_{Guid.NewGuid():N}";

    static CategoryWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=dummy_test;Username=test;Password=test");
        Environment.SetEnvironmentVariable("MinIO__Endpoint", "localhost:9000");
        Environment.SetEnvironmentVariable("MinIO__AccessKey", "test");
        Environment.SetEnvironmentVariable("MinIO__SecretKey", "test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Không phải Development => bỏ qua nhánh Migrate/seed của Program.cs,
        // API test không cần PostgreSQL thật.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=dummy_test;Username=test;Password=test" },
                { "Jwt:Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!" },
                { "MinIO:Endpoint", "localhost:9000" },
                { "MinIO:AccessKey", "test" },
                { "MinIO:SecretKey", "test" }
            });
        });

        builder.ConfigureServices(services =>
        {
            var optionsDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (optionsDescriptor is not null)
            {
                services.Remove(optionsDescriptor);
            }

            var contextDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(ApplicationDbContext));
            if (contextDescriptor is not null)
            {
                services.Remove(contextDescriptor);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            var cacheDescriptor = services.SingleOrDefault(
                descriptor => descriptor.ServiceType == typeof(ICategoryCache));
            if (cacheDescriptor is not null)
            {
                services.Remove(cacheDescriptor);
            }

            services.AddSingleton<ICategoryCache, TestCategoryCache>();
        });
    }

    public async Task SeedAsync(Func<IApplicationDbContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        await seed(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }

    public T Query<T>(Func<IApplicationDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }
}

internal sealed class TestCategoryCache : ICategoryCache
{
    private readonly Dictionary<string, object?> entries = new(StringComparer.Ordinal);

    public int InvalidateCount { get; private set; }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(entries.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        entries[key] = value;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        InvalidateCount++;
        entries.Clear();
        return Task.CompletedTask;
    }
}
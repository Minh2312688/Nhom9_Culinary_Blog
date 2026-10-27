using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// OT-02: EF Core activity test on SQLite.
/// </summary>
[Collection("OpenTelemetryTracing")]
public sealed class EfCoreActivityTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private List<Activity> _activities = null!;
    private SqliteConnection _connection = null!;

    static EfCoreActivityTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=observability_test;Username=test;Password=test");
    }

    public Task InitializeAsync()
    {
        _activities = new List<Activity>();
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

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
                // Remove existing DbContextOptions and ApplicationDbContext registrations
                var optionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>));
                if (optionsDescriptor != null) services.Remove(optionsDescriptor);

                var contextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext));
                if (contextDescriptor != null) services.Remove(contextDescriptor);

                // Remove IDbContextOptionsConfiguration for ApplicationDbContext
                var configDescriptors = services.Where(
                    d => d.ServiceType == typeof(IDbContextOptionsConfiguration<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>)).ToList();
                foreach (var desc in configDescriptors) services.Remove(desc);

                // Register SQLite with the open connection
                services.AddDbContext<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>(options =>
                    options.UseSqlite(_connection));

                services.ConfigureOpenTelemetryTracerProvider(tp => tp.AddProcessor(new CollectingActivityProcessor(_activities)));
            });
        });

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _connection.DisposeAsync();
    }

    [Fact]
    public void EfCoreActivity_SqliteSchemaCreation_AttemptsOnce()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CulinaryBlog.Infrastructure.Persistence.ApplicationDbContext>();

        // Make ONE attempt at EnsureCreated
        Exception? ensureCreatedException = null;
        string? exceptionType = null;
        try
        {
            db.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            ensureCreatedException = ex;
            exceptionType = ex.GetType().FullName;
        }

        if (ensureCreatedException != null)
        {
            // BLOCKED: model not creatable on SQLite - OT-09 and OT-10 already prove tag stripping
            // Verify the exception is actually a model/provider incompatibility
            var message = ensureCreatedException.Message;
            var isModelIncompatibility = ensureCreatedException is InvalidOperationException ||
                message.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("provider", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("model", StringComparison.OrdinalIgnoreCase);

            if (!isModelIncompatibility)
            {
                throw new Exception($"EnsureCreated failed with unexpected exception: {exceptionType}. Message: {message}");
            }

            // Record blocker with exception type
            throw new Exception($"BLOCKED: model not creatable on SQLite - OT-09 and OT-10 already prove tag stripping. Exception type: {exceptionType}. Message: {message}");
        }

        const string sentinel = "SENTINEL-PARAM-123";
        _activities.Clear();
        // Use a parameterized query with sentinel value
        var categories = db.Categories.Where(c => c.Name == sentinel).ToList();

        // Wait for activity recording
        Thread.Sleep(1000);

        // Assert EF Core activity exists
        var efActivities = _activities.Where(a =>
            a.Source.Name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) ||
            a.DisplayName.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
            a.Tags.Any(t => t.Key.StartsWith("db.", StringComparison.OrdinalIgnoreCase))).ToList();

        Assert.NotEmpty(efActivities);

        // Assert no tag contains SQL text, parameter value, or sentinel
        foreach (var activity in _activities)
        {
            foreach (var tag in activity.Tags)
            {
                Assert.False(tag.Key == "db.statement", $"Found db.statement tag: {tag.Value}");
                Assert.False(tag.Key == "db.query.text", $"Found db.query.text tag: {tag.Value}");
                if (tag.Value != null)
                {
                    var valueStr = tag.Value.ToString()!;
                    Assert.DoesNotContain(sentinel, valueStr, StringComparison.OrdinalIgnoreCase);
                    // Check for SQL text leakage
                    Assert.DoesNotContain("SELECT", valueStr, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("FROM", valueStr, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("WHERE", valueStr, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("INSERT", valueStr, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("UPDATE", valueStr, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("DELETE", valueStr, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}

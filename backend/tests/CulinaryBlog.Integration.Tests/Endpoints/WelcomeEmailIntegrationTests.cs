using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

/// <summary>
/// FR-JOB-001 integration: proves HTTP register triggers the welcome-email
/// enqueue with (userId, email, displayName), and that duplicate register
/// does not enqueue. Runs in "Testing" environment so Hangfire/PostgreSQL
/// storage and SMTP are skipped; a spy replaces IWelcomeEmailEnqueuer.
/// </summary>
public sealed class WelcomeEmailWebApplicationFactory : WebApplicationFactory<Program>
{
    static WelcomeEmailWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=dummy_test;Username=test;Password=test");
    }

    private readonly string _dbName = $"WelcomeEmailApiTestDb_{Guid.NewGuid():N}";

    public EnqueueSpy Spy { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing": skips Development migrate/seed and Hangfire registration.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=dummy_test;Username=test;Password=test" },
                { "Jwt:Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!" }
            });
        });

        builder.ConfigureServices(services =>
        {
            var optionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (optionsDescriptor != null)
            {
                services.Remove(optionsDescriptor);
            }

            var contextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (contextDescriptor != null)
            {
                services.Remove(contextDescriptor);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            var enqueuerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IWelcomeEmailEnqueuer));
            if (enqueuerDescriptor != null)
            {
                services.Remove(enqueuerDescriptor);
            }

            services.AddSingleton<IWelcomeEmailEnqueuer>(Spy);
        });
    }

    public sealed class EnqueueSpy : IWelcomeEmailEnqueuer
    {
        public ConcurrentBag<(string UserId, string Email, string DisplayName)> Calls { get; } = new();

        public Task EnqueueWelcomeEmailAsync(
            string userId,
            string email,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((userId, email, displayName));
            return Task.CompletedTask;
        }
    }
}

public sealed class WelcomeEmailIntegrationTests : IClassFixture<WelcomeEmailWebApplicationFactory>
{
    private readonly WelcomeEmailWebApplicationFactory _factory;

    public WelcomeEmailIntegrationTests(WelcomeEmailWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ThroughHttp_ShouldEnqueueWelcomeEmail_WithUserIdEmailDisplayName()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = $"welcome_{Guid.NewGuid():N}@example.com";
        const string displayName = "Welcome Chef";
        var before = _factory.Spy.Calls.Count;

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto(email, "P@ssword123", displayName));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponseDto>();
        body.Should().NotBeNull();

        _factory.Spy.Calls.Should().HaveCount(before + 1);
        var call = _factory.Spy.Calls.Last();
        call.UserId.Should().Be(body!.UserId);
        call.Email.Should().Be(email);
        call.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldNotEnqueueWelcomeEmail()
    {
        // Arrange
        var client = _factory.CreateClient();
        var email = $"dup_{Guid.NewGuid():N}@example.com";
        var first = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto(email, "P@ssword123", "First Chef"));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var before = _factory.Spy.Calls.Count;

        // Act
        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequestDto(email, "P@ssword456", "Second Chef"));

        // Assert
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        _factory.Spy.Calls.Should().HaveCount(before);
    }
}

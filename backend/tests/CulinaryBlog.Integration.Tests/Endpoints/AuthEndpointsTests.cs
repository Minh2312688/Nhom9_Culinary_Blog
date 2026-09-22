using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Endpoints;

public class CustomAuthWebApplicationFactory : WebApplicationFactory<Program>
{
    static CustomAuthWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "TestSecretKeyForIntegrationTestingOnly_MustBeAtLeast256Bits!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=localhost;Port=5432;Database=dummy_test;Username=test;Password=test");
    }

    private readonly string _dbName = $"CulinaryBlogTestDb_{Guid.NewGuid():N}";
    public Mock<IGoogleTokenValidator> GoogleTokenValidatorMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
            // Move test database provider into integration test DI override (SRS Compliance)
            var dbContextOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
            if (dbContextOptionsDescriptor != null)
            {
                services.Remove(dbContextOptionsDescriptor);
            }

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AuthDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<AuthDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            var googleDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IGoogleTokenValidator));
            if (googleDescriptor != null)
            {
                services.Remove(googleDescriptor);
            }

            services.AddSingleton(GoogleTokenValidatorMock.Object);
        });
    }

    public HttpClient CreateIsolatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", $"192.168.1.{Guid.NewGuid():N}");
        return client;
    }
}

public class AuthEndpointsTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public AuthEndpointsTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ValidPayload_ShouldReturn201Created()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();
        var request = new RegisterRequestDto(
            Email: $"test_{Guid.NewGuid():N}@example.com",
            Password: "P@ssword123",
            DisplayName: "Test Chef");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<RegisterResponseDto>();
        result.Should().NotBeNull();
        result!.UserId.Should().NotBeNullOrWhiteSpace();
        result.Email.Should().Be(request.Email);
        result.DisplayName.Should().Be(request.DisplayName);
    }

    [Fact]
    public async Task Register_InvalidPayload_ShouldReturn400BadRequest()
    {
        // Arrange - missing email & weak password
        var client = _factory.CreateIsolatedClient();
        var request = new RegisterRequestDto(
            Email: "not-an-email",
            Password: "weak",
            DisplayName: "");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldReturn409Conflict()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequestDto(email, "P@ssword123", "First User");
        var firstResponse = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - register again with same email
        var duplicateRequest = new RegisterRequestDto(email, "P@ssword456", "Second User");
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", duplicateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturn200Ok_WithTokens()
    {
        // Arrange: Register first
        var client = _factory.CreateIsolatedClient();
        var email = $"login_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Login Chef"));

        // Act
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();
        var loginRequest = new LoginRequestDto("nonexistent@example.com", "WrongPassword123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_ShouldReturn423Locked()
    {
        // Arrange: Register a fresh user
        var client = _factory.CreateIsolatedClient();
        var email = $"lockout_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Lockout Target"));

        // Act: Fail 5 times
        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, "WrongPassword!"));
        }

        // 6th attempt: must return 423 Locked
        var lockedResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, "WrongPassword!"));

        // Assert
        lockedResponse.StatusCode.Should().Be((HttpStatusCode)423);
    }

    [Fact]
    public async Task GoogleLogin_ValidToken_ShouldReturn200Ok_WithTokens()
    {
        // Arrange: Mock google validator
        var client = _factory.CreateIsolatedClient();
        var idToken = "valid_google_token_" + Guid.NewGuid().ToString("N");
        var googleEmail = $"google_{Guid.NewGuid():N}@gmail.com";

        _factory.GoogleTokenValidatorMock
            .Setup(x => x.ValidateAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleTokenInfo("google_sub_" + Guid.NewGuid().ToString("N"), googleEmail, "Google Chef", null));

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequestDto(idToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GoogleLogin_InvalidToken_ShouldReturn400BadRequest()
    {
        // Arrange: Mock google validator returning null
        var client = _factory.CreateIsolatedClient();
        var idToken = "invalid_token_" + Guid.NewGuid().ToString("N");
        _factory.GoogleTokenValidatorMock
            .Setup(x => x.ValidateAsync(idToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GoogleTokenInfo?)null);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/google", new GoogleLoginRequestDto(idToken));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RateLimiting_ExceedingLimit_ShouldReturn429TooManyRequests()
    {
        // Arrange: Create a dedicated client with a fixed IP
        var client = _factory.CreateClient();
        var fixedIp = "10.0.0.99";
        client.DefaultRequestHeaders.Add("X-Test-Client-IP", fixedIp);

        // Act: Send 10 requests (the limit)
        for (int i = 0; i < 10; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto("test@example.com", "Password123!"));
        }

        // 11th request exceeds 10 req/min limit
        var exceededResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto("test@example.com", "Password123!"));

        // Assert
        exceededResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        exceededResponse.Headers.Contains("Retry-After").Should().BeTrue();
    }
}

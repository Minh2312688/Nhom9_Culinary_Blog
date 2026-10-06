using System.Net;
using System.Net.Http.Headers;
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
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextOptionsDescriptor != null)
            {
                services.Remove(dbContextOptionsDescriptor);
            }

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ApplicationDbContext));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ApplicationDbContext>(options =>
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

    [Fact]
    public async Task RefreshToken_ValidToken_ShouldReturn200Ok_WithNewTokens()
    {
        // Arrange: Register and login to obtain initial tokens
        var client = _factory.CreateIsolatedClient();
        var email = $"refresh_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Refresh User"));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResult.Should().NotBeNull();

        // Act: Refresh using raw refresh token
        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(authResult!.RefreshToken));

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newAuthResult = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        newAuthResult.Should().NotBeNull();
        newAuthResult!.AccessToken.Should().NotBeNullOrWhiteSpace();
        newAuthResult.RefreshToken.Should().NotBeNullOrWhiteSpace();
        newAuthResult.RefreshToken.Should().NotBe(authResult.RefreshToken);
        newAuthResult.ExpiresIn.Should().Be(900);
    }

    [Fact]
    public async Task RefreshToken_ReuseOldToken_ShouldReturn401Unauthorized()
    {
        // Arrange: Register and login to obtain tokens
        var client = _factory.CreateIsolatedClient();
        var email = $"reuse_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Reuse User"));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // First refresh: succeeds and rotates
        var firstRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(authResult!.RefreshToken));
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act: Attempt second refresh with same old token (reuse)
        var secondRefresh = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(authResult.RefreshToken));

        // Assert: MUST return 401 Unauthorized
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto("completely_invalid_random_token_12345"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_Authenticated_ShouldReturn200Ok_WithSafeFields()
    {
        // Arrange: Register and login
        var client = _factory.CreateIsolatedClient();
        var email = $"profile_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";
        var displayName = "Profile Chef";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, displayName));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Act: Request profile with Bearer access token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);
        var meResponse = await client.GetAsync("/api/v1/auth/me");

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await meResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(email);
        profile.DisplayName.Should().Be(displayName);
        profile.Roles.Should().NotBeEmpty();

        // Security check: raw JSON response must not contain sensitive Identity fields
        var rawJson = await meResponse.Content.ReadAsStringAsync();
        rawJson.ToLowerInvariant().Should().NotContain("passwordhash");
        rawJson.ToLowerInvariant().Should().NotContain("securitystamp");
        rawJson.ToLowerInvariant().Should().NotContain("concurrencystamp");
        rawJson.ToLowerInvariant().Should().NotContain("tokenhash");
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange: Client without Authorization header
        var client = _factory.CreateIsolatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Authenticated_ShouldReturn204NoContent_AndRevokeRefreshToken()
    {
        // Arrange: Register and login
        var client = _factory.CreateIsolatedClient();
        var email = $"logout_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Logout User"));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        // Act: Call logout with Bearer token and refreshToken in body
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);
        var logoutResponse = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequestDto(authResult.RefreshToken));

        // Assert: 204 No Content
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify: Refresh token is now revoked and cannot be refreshed
        var refreshAttempt = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(authResult.RefreshToken));
        refreshAttempt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Idempotent_SecondCall_ShouldStillReturn204NoContent()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();
        var email = $"logout_idem_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Idem User"));
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        // First logout: 204
        var first = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequestDto(authResult.RefreshToken));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Second logout with same token: must still return 204 (Idempotent)
        var second = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequestDto(authResult.RefreshToken));
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_Unauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange: No Bearer header
        var client = _factory.CreateIsolatedClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequestDto("some_token"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_ConcurrentSimultaneousRequests_ExactlyOneMustSucceed()
    {
        // Arrange: Register and login to obtain initial refresh token
        var client1 = _factory.CreateIsolatedClient();
        var client2 = _factory.CreateIsolatedClient();
        var email = $"concurrent_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client1.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Concurrent User"));
        var loginResponse = await client1.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResult.Should().NotBeNull();
        var sharedRefreshToken = authResult!.RefreshToken;

        // Act: Send two simultaneous refresh requests using the exact same refresh token
        var task1 = client1.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(sharedRefreshToken));
        var task2 = client2.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(sharedRefreshToken));

        var responses = await Task.WhenAll(task1, task2);

        // Assert: Exactly one HTTP 200 OK and exactly one HTTP 401 Unauthorized
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        statusCodes.Should().ContainSingle(s => s == HttpStatusCode.OK);
        statusCodes.Should().ContainSingle(s => s == HttpStatusCode.Unauthorized);

        // Verify database: only ONE active child refresh token exists for this user, and old token is revoked
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().NotBeNull();

        var userTokens = await db.RefreshTokens.Where(t => t.UserId == user!.Id).ToListAsync();
        var activeTokens = userTokens.Where(t => t.RevokedAt == null).ToList();
        activeTokens.Should().HaveCount(1, "only one valid child refresh token should have been issued");

        var revokedTokens = userTokens.Where(t => t.RevokedAt != null).ToList();
        revokedTokens.Should().ContainSingle(t => t.ReplacedByTokenHash == activeTokens[0].TokenHash,
            "old token must be revoked and reference the single active child token");
    }

    [Fact]
    public async Task UpdateProfile_Authenticated_ValidPayload_ShouldReturn200Ok_AndPersistToDatabase()
    {
        // Arrange: Register and login
        var client = _factory.CreateIsolatedClient();
        var email = $"patch_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";
        var originalName = "Initial Chef";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, originalName));
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        var updatePayload = new UpdateProfileRequestDto(
            DisplayName: "Master Chef Nam",
            AvatarUrl: "https://example.com/chef-nam.jpg",
            Bio: "Passionate Vietnamese culinary specialist.");

        // Act
        var patchResponse = await client.PatchAsJsonAsync("/api/v1/auth/me", updatePayload);

        // Assert 1: HTTP 200 OK
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await patchResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be(updatePayload.DisplayName);
        profile.AvatarUrl.Should().Be(updatePayload.AvatarUrl);
        profile.Bio.Should().Be(updatePayload.Bio);
        profile.Email.Should().Be(email);

        // Assert 2: Database actually contains updated values
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        dbUser.Should().NotBeNull();
        dbUser!.DisplayName.Should().Be(updatePayload.DisplayName);
        dbUser.AvatarUrl.Should().Be(updatePayload.AvatarUrl);
        dbUser.Bio.Should().Be(updatePayload.Bio);
    }

    [Fact]
    public async Task UpdateProfile_PartialUpdate_OmittedFieldsMustRemainUnchanged()
    {
        // Arrange: Register and login
        var client = _factory.CreateIsolatedClient();
        var email = $"partial_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "First Name"));
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        // First update: set avatar and bio
        var setupPayload = new UpdateProfileRequestDto(
            DisplayName: "Chef Stage 1",
            AvatarUrl: "https://example.com/stage1.jpg",
            Bio: "Original Bio Content");
        await client.PatchAsJsonAsync("/api/v1/auth/me", setupPayload);

        // Act: Partial update providing ONLY DisplayName (avatarUrl and bio are null/omitted)
        var partialPayload = new UpdateProfileRequestDto(
            DisplayName: "Chef Stage 2",
            AvatarUrl: null,
            Bio: null);
        var response = await client.PatchAsJsonAsync("/api/v1/auth/me", partialPayload);

        // Assert: DisplayName updated, AvatarUrl and Bio retained
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        profile.Should().NotBeNull();
        profile!.DisplayName.Should().Be("Chef Stage 2");
        profile.AvatarUrl.Should().Be("https://example.com/stage1.jpg");
        profile.Bio.Should().Be("Original Bio Content");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        dbUser!.DisplayName.Should().Be("Chef Stage 2");
        dbUser.AvatarUrl.Should().Be("https://example.com/stage1.jpg");
        dbUser.Bio.Should().Be("Original Bio Content");
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_ShouldReturn401Unauthorized()
    {
        // Arrange: client without Bearer token
        var client = _factory.CreateIsolatedClient();
        var payload = new UpdateProfileRequestDto("Sneaky Update", null, null);

        // Act
        var response = await client.PatchAsJsonAsync("/api/v1/auth/me", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_InvalidInput_ShouldReturn400ProblemDetails()
    {
        // Arrange: Login valid user
        var client = _factory.CreateIsolatedClient();
        var email = $"invalid_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Valid User"));
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        // Case A: Empty request body (all fields null)
        var emptyPayload = new UpdateProfileRequestDto(null, null, null);
        var resEmpty = await client.PatchAsJsonAsync("/api/v1/auth/me", emptyPayload);
        resEmpty.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Case B: Invalid Avatar URL scheme
        var invalidUrlPayload = new UpdateProfileRequestDto("Valid Name", "not-a-valid-url", null);
        var resInvalidUrl = await client.PatchAsJsonAsync("/api/v1/auth/me", invalidUrlPayload);
        resInvalidUrl.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_CannotChangeEmailOrUserName_ThroughEndpoint()
    {
        // Arrange
        var client = _factory.CreateIsolatedClient();
        var email = $"immutable_{Guid.NewGuid():N}@example.com";
        var password = "P@ssword123!";

        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequestDto(email, password, "Immutable User"));
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var authResult = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authResult!.AccessToken);

        // Act: Attempt to send email and userName via raw JSON
        var rawJson = "{\"displayName\":\"Safe New Name\",\"email\":\"hacker@evil.com\",\"userName\":\"evil_user\"}";
        var content = new StringContent(rawJson, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PatchAsync("/api/v1/auth/me", content);

        // Assert: Endpoint updates displayName, but does NOT modify email or userName
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userInDb = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        userInDb.Should().NotBeNull();
        userInDb!.DisplayName.Should().Be("Safe New Name");
        userInDb.Email.Should().Be(email);
        userInDb.UserName.Should().Be(email);

        var hackerUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "hacker@evil.com");
        hackerUser.Should().BeNull();
    }
}

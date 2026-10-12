using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using CulinaryBlog.API;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Infrastructure.Identity;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Integration.Tests.Endpoints;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Security;

/// <summary>
/// NFR-SEC-002 Integration Tests for Refresh Token Rotation, Storage, and Full Family Revocation:
/// - TTL is 7 days.
/// - Database stores SHA-256 hash (raw token is never persisted).
/// - Atomic one-time rotation.
/// - Full descendant token family revocation upon reuse attack.
/// - Independent families for the same user remain valid.
/// </summary>
public class RefreshTokenFamilyRevocationTests : IClassFixture<CustomAuthWebApplicationFactory>
{
    private readonly CustomAuthWebApplicationFactory _factory;

    public RefreshTokenFamilyRevocationTests(CustomAuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string email, string password)> CreateTestUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"token_test_{uniqueId}@example.com";
        var password = $"P@ssword_{uniqueId}#123";

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            DisplayName = $"Token User {uniqueId}",
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue();

        if (!await roleManager.RoleExistsAsync("Author"))
        {
            await roleManager.CreateAsync(new IdentityRole("Author"));
        }
        await userManager.AddToRoleAsync(user, "Author");

        return (email, password);
    }

    [Fact]
    public async Task RefreshToken_Storage_ShouldHave7DayTtl_AndPersistSha256Hash()
    {
        // Arrange
        var (email, password) = await CreateTestUserAsync();
        var client = _factory.CreateIsolatedClient();

        // Act: Login to generate initial refresh token
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        loginResult.Should().NotBeNull();
        var rawRefreshToken = loginResult!.RefreshToken;

        // Query database for persisted token entity
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var expectedHash = jwtGenerator.HashRefreshToken(rawRefreshToken);
        var tokenEntity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == expectedHash);

        // Assert
        tokenEntity.Should().NotBeNull("Refresh token must be stored in DB by hash");
        tokenEntity!.TokenHash.Should().NotBe(rawRefreshToken, "Raw token must never be stored in database");
        tokenEntity.TokenHash.Should().Be(expectedHash, "Token hash in DB must match SHA-256 of raw token");

        // TTL approximately 7 days (within 2 minutes tolerance)
        var expectedExpiry = DateTimeOffset.UtcNow.AddDays(7);
        tokenEntity.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(2),
            "NFR-SEC-002 requires refresh token TTL of 7 days");

        tokenEntity.RevokedAt.Should().BeNull();
        tokenEntity.ReplacedByTokenHash.Should().BeNull();
    }

    [Fact]
    public async Task Rotation_ValidToken_ShouldRevokeOldToken_AndAllowNewTokenUsage()
    {
        // Arrange
        var (email, password) = await CreateTestUserAsync();
        var client = _factory.CreateIsolatedClient();

        // Step 1: Login -> T1
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var t1 = (await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        // Step 2: Rotate T1 -> T2
        var rotateResp1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(t1));
        rotateResp1.StatusCode.Should().Be(HttpStatusCode.OK);
        var t2 = (await rotateResp1.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        t2.Should().NotBe(t1, "Rotated token must be distinct from old token");

        // Step 3: Verify T1 is revoked and replaced in DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

            var t1Hash = jwt.HashRefreshToken(t1);
            var t2Hash = jwt.HashRefreshToken(t2);

            var t1Entity = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == t1Hash);
            t1Entity.Should().NotBeNull();
            t1Entity!.RevokedAt.Should().NotBeNull("Used refresh token must be revoked immediately");
            t1Entity.ReplacedByTokenHash.Should().Be(t2Hash, "Old token must reference replacing token hash");
        }

        // Step 4: Rotate T2 -> T3 (T2 must be usable)
        var rotateResp2 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(t2));
        rotateResp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var t3 = (await rotateResp2.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;
        t3.Should().NotBe(t2);
    }

    [Fact]
    public async Task FullFamilyRevocation_WhenOldTokenIsReused_ShouldRevokeEntireDescendantLineage()
    {
        // Scenario: A -> B -> C. Attacker reuses A.
        // Expected: A is rejected; B and C are revoked; C can no longer refresh.
        var (email, password) = await CreateTestUserAsync();
        var client = _factory.CreateIsolatedClient();

        // 1. Initial Login -> A
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var tokenA = (await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        // 2. Legitimate Rotation: A -> B
        var refreshResp1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(tokenA));
        var tokenB = (await refreshResp1.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        // 3. Legitimate Rotation: B -> C
        var refreshResp2 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(tokenB));
        var tokenC = (await refreshResp2.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        // 4. ATTACK: Adversary reuses compromised token A
        var reuseResp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(tokenA));
        reuseResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Reusing an already revoked/replaced token must be rejected with 401");

        // 5. Verify in DB that ALL descendants (B and C) are now revoked
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

            var hashB = jwt.HashRefreshToken(tokenB);
            var hashC = jwt.HashRefreshToken(tokenC);

            var entityB = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hashB);
            var entityC = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hashC);

            entityB.Should().NotBeNull();
            entityB!.RevokedAt.Should().NotBeNull("Descendant B must be revoked upon reuse of A");

            entityC.Should().NotBeNull();
            entityC!.RevokedAt.Should().NotBeNull("Descendant C must be revoked upon reuse of A");
        }

        // 6. Legitimate client tries to use latest token C -> must be rejected
        var clientTryC = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(tokenC));
        clientTryC.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "Compromised family's active token C must be rejected because family was revoked");
    }

    [Fact]
    public async Task FullFamilyRevocation_ShouldNotRevokeIndependentFamily_ForSameUser()
    {
        // Scenario: Same user has two sessions / devices:
        // Family 1: T1 -> T2 -> T3
        // Family 2: X1 -> X2
        // Adversary reuses T1 -> Family 1 (T2, T3) revoked.
        // Family 2 (X2) must remain valid and able to rotate!
        var (email, password) = await CreateTestUserAsync();
        var client = _factory.CreateIsolatedClient();

        // 1. Session 1 (Device 1): Login -> T1 -> T2 -> T3
        var login1 = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var t1 = (await login1.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        var rotT1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(t1));
        var t2 = (await rotT1.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        var rotT2 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(t2));
        var t3 = (await rotT2.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        // 2. Session 2 (Device 2): Login -> X1 -> X2
        var login2 = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        var x1 = (await login2.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        var rotX1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(x1));
        var x2 = (await rotX1.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;

        // 3. Attack: Reuse T1 on Family 1
        var reuseT1 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(t1));
        reuseT1.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 4. Assert: Family 1 is revoked (T3 rejected)
        var tryT3 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(t3));
        tryT3.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "Family 1 was compromised and revoked");

        // 5. Assert: Independent Family 2 (X2) remains valid and can rotate successfully!
        var tryX2 = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequestDto(x2));
        tryX2.StatusCode.Should().Be(HttpStatusCode.OK,
            "Independent session/family X2 for the same user must NOT be revoked");

        var x3 = (await tryX2.Content.ReadFromJsonAsync<AuthResponseDto>())!.RefreshToken;
        x3.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AccessToken_Claims_ShouldBeCompatibleWithCurrentUserService_AndIsInRole()
    {
        // Arrange
        var (email, password) = await CreateTestUserAsync();
        var client = _factory.CreateIsolatedClient();

        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequestDto(email, password));
        loginResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResult = await loginResp.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResult.Should().NotBeNull();

        // 1. Decode raw JWT payload
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(authResult!.AccessToken);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub);
        jwtToken.Claims.Should().Contain(c => c.Type == "userId");
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        jwtToken.Claims.Should().Contain(c => c.Type == "roles" && c.Value == "Author");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Author");

        // 2. Direct CurrentUserService verification
        var identity = new ClaimsIdentity(jwtToken.Claims, "Bearer", ClaimTypes.NameIdentifier, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(httpContextAccessor);

        currentUserService.UserId.Should().NotBeNullOrWhiteSpace();
        currentUserService.IsAuthenticated.Should().BeTrue();
        principal.IsInRole("Author").Should().BeTrue();

        // 3. End-to-end /api/v1/auth/me call
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        var meResp = await client.GetAsync("/api/v1/auth/me");
        meResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await meResp.Content.ReadFromJsonAsync<UserProfileDto>();
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(email);
    }
}

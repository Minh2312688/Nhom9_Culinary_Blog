using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Security;

/// <summary>
/// NFR-SEC-002 Security Tests:
/// - Access Token: HS256, 15m TTL, claims (sub, email, jti, role) + SRS compatibility aliases (userId, roles).
/// - Refresh Token: 512-bit cryptographically secure random entropy (CONFLICT-018 OPEN), SHA-256 hash storage.
/// </summary>
public class JwtTokenSecurityTests
{
    private const string TestKey = "TestSecretKeyForTestingOnly_Min256BitsLongSecretKey!";
    private const string TestIssuer = "CulinaryBlogTest";
    private const string TestAudience = "CulinaryBlogAppTest";

    private readonly JwtService _jwtService;

    public JwtTokenSecurityTests()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", TestKey },
                { "Jwt:Issuer", TestIssuer },
                { "Jwt:Audience", TestAudience }
            })
            .Build();

        _jwtService = new JwtService(configuration);
    }

    [Fact]
    public void GenerateAccessToken_ShouldUseHS256_AndHave15MinuteTtl()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var email = "chef@example.com";
        var roles = new[] { "Author" };

        // Act
        var tokenString = _jwtService.GenerateAccessToken(userId, email, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(tokenString).Should().BeTrue();

        var token = handler.ReadJwtToken(tokenString);

        // Algorithm must be HS256 (HmacSha256)
        token.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256,
            "NFR-SEC-002 requires HS256 signing algorithm");

        // TTL approximately 15 minutes (within 30 seconds tolerance)
        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);
        token.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(30),
            "NFR-SEC-002 requires 15 minutes TTL");
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainOperationalClaims_AndSrsCompatibilityAliases()
    {
        // Arrange
        var userId = "user-123-uuid";
        var email = "user@culinaryblog.com";
        var roles = new[] { "Admin", "Author" };

        // Act
        var tokenString = _jwtService.GenerateAccessToken(userId, email, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // 1. Operational claims
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId,
            "Operational 'sub' claim must contain userId for ASP.NET Identity mapping");

        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email,
            "Operational 'email' claim must be present");

        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti && !string.IsNullOrWhiteSpace(c.Value),
            "Operational 'jti' claim must be present");

        token.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)
            .Should().BeEquivalentTo(roles,
            "Operational role claims must be emitted for ASP.NET authorization");

        // 2. SRS NFR-SEC-002 compatibility aliases
        token.Claims.Should().Contain(c => c.Type == "userId" && c.Value == userId,
            "SRS alias 'userId' must be present in token payload");

        token.Claims.Where(c => c.Type == "roles").Select(c => c.Value)
            .Should().BeEquivalentTo(roles,
            "SRS alias 'roles' must contain all assigned roles");

        // 3. Operational authorization compatibility: User.IsInRole and UserId resolution
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateLifetime = false
        };

        var principal = handler.ValidateToken(tokenString, validationParameters, out _);
        principal.IsInRole("Admin").Should().BeTrue("User.IsInRole('Admin') must succeed");
        principal.IsInRole("Author").Should().BeTrue("User.IsInRole('Author') must succeed");
        principal.IsInRole("SuperAdmin").Should().BeFalse();

        var resolvedUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        resolvedUserId.Should().Be(userId, "UserId resolution via NameIdentifier or sub must match");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldProduce512BitEntropy_AndSha256HashForStorage()
    {
        // Act
        var (rawToken, tokenHash) = _jwtService.GenerateRefreshToken();

        // Assert 1: Raw token entropy is 512 bits (64 bytes Base64)
        var rawBytes = Convert.FromBase64String(rawToken);
        rawBytes.Length.Should().Be(64,
            "CONFLICT-018: Refresh token maintains 512-bit (64 bytes) cryptographically secure entropy");

        // Assert 2: Stored hash is SHA-256 of raw token (64 hex characters)
        rawToken.Should().NotBe(tokenHash,
            "Raw refresh token must never equal the database token hash");

        var expectedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        var expectedHashHex = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        tokenHash.Should().Be(expectedHashHex,
            "Database token hash must be valid SHA-256 hex string");

        tokenHash.Length.Should().Be(64,
            "SHA-256 hash string must be exactly 64 hexadecimal characters");
    }

    [Fact]
    public void HashRefreshToken_ShouldBeDeterministic()
    {
        // Arrange
        var (rawToken, initialHash) = _jwtService.GenerateRefreshToken();

        // Act
        var computedHash = _jwtService.HashRefreshToken(rawToken);

        // Assert
        computedHash.Should().Be(initialHash,
            "Hashing the same raw refresh token must always yield identical hash");
    }
}

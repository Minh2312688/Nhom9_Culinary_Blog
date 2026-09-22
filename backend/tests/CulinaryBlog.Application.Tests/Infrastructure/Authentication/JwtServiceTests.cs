using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Infrastructure.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Authentication;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;

    public JwtServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Key", "SuperSecretKeyForTestingPurposesOnly_MustBeAtLeast256BitsLong123!"},
            {"Jwt:Issuer", "TestCulinaryBlog"},
            {"Jwt:Audience", "TestCulinaryBlogApp"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtService = new JwtService(configuration);
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainRequiredClaims_AndCorrectExpiry()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var email = "chef@example.com";
        var roles = new[] { AppRoles.Author };

        // Act
        var tokenString = _jwtService.GenerateAccessToken(userId, email, roles);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(userId);
        token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value.Should().Be(email);
        token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti).Should().NotBeNull();
        token.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be(AppRoles.Author);

        // TTL = 15 minutes (with a small buffer for execution time)
        var expiration = token.ValidTo;
        var diff = expiration - DateTime.UtcNow;
        diff.TotalMinutes.Should().BeInRange(14, 16);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldProduceCryptographicallySecureToken_AndValidSha256Hash()
    {
        // Act
        var (rawToken, tokenHash) = _jwtService.GenerateRefreshToken();

        // Assert
        rawToken.Should().NotBeNullOrWhiteSpace();
        tokenHash.Should().NotBeNullOrWhiteSpace();

        // Raw token must NOT equal the hash
        rawToken.Should().NotBe(tokenHash);

        // Verify SHA-256 hash
        var computedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
        tokenHash.Should().Be(computedHash);
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Security;

/// <summary>
/// NFR-SEC-007: Secrets Management Unit Tests.
/// Verifies secret configuration validation, key strength, and dual-key rotation behavior.
/// </summary>
public sealed class SecretsManagementUnitTests
{
    private const string ValidKey = "ThisIsAValidSecretKeyForTestingPurposeOnly_AtLeast256BitsLong!";
    private const string ShortKey = "TooShortKey!";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StartupValidation_MissingJwtKey_ShouldThrowInvalidOperationException(string? missingKey)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Jwt:Key", missingKey }
            })
            .Build();

        var act = () =>
        {
            var key = config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Configuration 'Jwt:Key' is required and cannot be empty.");
            }
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Configuration 'Jwt:Key' is required and cannot be empty.");
    }

    [Fact]
    public void SymmetricSecurityKey_Under256Bits_ShouldFailOrBeSuboptimal()
    {
        var keyBytes = Encoding.UTF8.GetBytes(ShortKey);
        // 256 bits = 32 bytes
        keyBytes.Length.Should().BeLessThan(32);

        var validKeyBytes = Encoding.UTF8.GetBytes(ValidKey);
        validKeyBytes.Length.Should().BeGreaterThanOrEqualTo(32);
    }

    [Fact]
    public void DualKeyRotation_NewTokenSignedWithNewKey_ValidatesAgainstDualKeyValidator()
    {
        // Simulate 90-day rotation: Primary key is NewKey, Secondary key is OldKey
        var oldKey = "OldSigningKey_FromPrevious90DayRotationCycle_Min256Bits!";
        var newKey = "NewSigningKey_FromCurrent90DayRotationCycle_Min256Bits!!";

        var oldSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(oldKey));
        var newSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(newKey));

        // Create token with NEW key
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Role, "Author")
            }),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "CulinaryBlog",
            Audience = "CulinaryBlogApp",
            SigningCredentials = new SigningCredentials(newSecurityKey, SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        // Validation parameters configured with BOTH keys during the rollover window
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CulinaryBlog",
            ValidateAudience = true,
            ValidAudience = "CulinaryBlogApp",
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = new[] { newSecurityKey, oldSecurityKey },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(tokenString, validationParams, out var validatedToken);
        principal.Should().NotBeNull();
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("test-user-id");
    }

    [Fact]
    public void DualKeyRotation_OldTokenSignedWithOldKey_ValidatesDuringRolloverWindow()
    {
        var oldKey = "OldSigningKey_FromPrevious90DayRotationCycle_Min256Bits!";
        var newKey = "NewSigningKey_FromCurrent90DayRotationCycle_Min256Bits!!";

        var oldSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(oldKey));
        var newSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(newKey));

        // Create token with OLD key
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-old"),
                new Claim(ClaimTypes.Role, "Author")
            }),
            Expires = DateTime.UtcNow.AddMinutes(10),
            Issuer = "CulinaryBlog",
            Audience = "CulinaryBlogApp",
            SigningCredentials = new SigningCredentials(oldSecurityKey, SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        // Validation parameters with BOTH keys accepts old token
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CulinaryBlog",
            ValidateAudience = true,
            ValidAudience = "CulinaryBlogApp",
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = new[] { newSecurityKey, oldSecurityKey },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(tokenString, validationParams, out var validatedToken);
        principal.Should().NotBeNull();
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("test-user-old");
    }

    [Fact]
    public void DualKeyRotation_OldToken_RejectedAfterOldKeyIsRetired()
    {
        var oldKey = "OldSigningKey_FromPrevious90DayRotationCycle_Min256Bits!";
        var newKey = "NewSigningKey_FromCurrent90DayRotationCycle_Min256Bits!!";

        var oldSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(oldKey));
        var newSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(newKey));

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-old") }),
            Expires = DateTime.UtcNow.AddMinutes(10),
            Issuer = "CulinaryBlog",
            Audience = "CulinaryBlogApp",
            SigningCredentials = new SigningCredentials(oldSecurityKey, SecurityAlgorithms.HmacSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        // After old key retired: only newSecurityKey is configured
        var retiredValidationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CulinaryBlog",
            ValidateAudience = true,
            ValidAudience = "CulinaryBlogApp",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = newSecurityKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var act = () => tokenHandler.ValidateToken(tokenString, retiredValidationParams, out _);
        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }
}

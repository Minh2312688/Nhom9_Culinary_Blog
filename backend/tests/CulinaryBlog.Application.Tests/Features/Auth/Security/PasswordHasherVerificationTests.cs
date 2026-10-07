using System.Buffers.Binary;
using System.Reflection;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Security;

/// <summary>
/// NFR-SEC-001 Verification Tests:
/// - Password complexity policy (>= 8 chars, uppercase, lowercase, digit, non-alphanumeric).
/// - PBKDF2-HMACSHA512 with iteration count >= 100,000 via ASP.NET Core Identity V3 payload format.
/// - Successful password verification.
/// - Plaintext password is never persisted or exposed on ApplicationUser.
/// </summary>
public class PasswordHasherVerificationTests
{
    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:Postgres", "Host=localhost;Database=culinary_test;Username=test;Password=test" },
                { "Jwt:Key", "TestSecretKeyForTestingOnly_Min256BitsLongSecretKey!" }
            })
            .Build();

        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void IdentityOptions_ShouldEnforcePasswordComplexityPolicy_AccordingToNfrSec001()
    {
        // Arrange
        using var provider = CreateServiceProvider();
        var identityOptions = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        // Assert - NFR-SEC-001 Acceptance Criteria
        identityOptions.Password.RequiredLength.Should().BeGreaterThanOrEqualTo(8,
            "NFR-SEC-001 requires password minimum length >= 8 characters");
        identityOptions.Password.RequireUppercase.Should().BeTrue(
            "NFR-SEC-001 requires at least one uppercase letter");
        identityOptions.Password.RequireLowercase.Should().BeTrue(
            "NFR-SEC-001 requires at least one lowercase letter");
        identityOptions.Password.RequireDigit.Should().BeTrue(
            "NFR-SEC-001 requires at least one digit");
        identityOptions.Password.RequireNonAlphanumeric.Should().BeTrue(
            "NFR-SEC-001 requires at least one special/non-alphanumeric character");
    }

    [Fact]
    public void PasswordHasher_ShouldGenerateIdentityV3Format_WithPbkdf2HmacSha512_AndIterationCountAtLeast100000()
    {
        // Arrange
        using var provider = CreateServiceProvider();
        var hasherOptions = provider.GetRequiredService<IOptions<PasswordHasherOptions>>();
        var hasher = new PasswordHasher<ApplicationUser>(hasherOptions);
        var user = new ApplicationUser { UserName = "test_sec_user", Email = "sec@example.com" };

        // Act - Hash a sample password (never logged or printed)
        var samplePassword = "P@ssword123Secure!";
        var hashedPassword = hasher.HashPassword(user, samplePassword);

        hashedPassword.Should().NotBeNullOrWhiteSpace();

        // Decode raw binary payload from Base64
        var payload = Convert.FromBase64String(hashedPassword);

        // Assert Microsoft Identity V3 Binary Format:
        // Byte 0: Format marker (0x01 = IdentityV3)
        // Bytes 1..4: KeyDerivationPrf (Big-Endian uint32): 0=HMACSHA1, 1=HMACSHA256, 2=HMACSHA512
        // Bytes 5..8: Iteration count (Big-Endian uint32)
        // Bytes 9..12: Salt size (Big-Endian uint32)
        // Bytes 13..13+SaltSize-1: Salt
        // Remaining bytes: PBKDF2 Subkey
        payload.Length.Should().BeGreaterThanOrEqualTo(13,
            "Identity V3 payload must contain at least 13 header bytes");

        var formatMarker = payload[0];
        formatMarker.Should().Be(0x01,
            "Header byte 0 must be 0x01 indicating Microsoft Identity V3 format");

        var prfId = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(1, 4));
        prfId.Should().Be(2,
            "KeyDerivationPrf identifier 2 corresponds to PBKDF2-HMACSHA512");

        var iterationCount = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(5, 4));
        iterationCount.Should().BeGreaterThanOrEqualTo(100000,
            "NFR-SEC-001 requires PBKDF2 iteration count >= 100,000");

        var saltSize = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(9, 4));
        saltSize.Should().BeGreaterThanOrEqualTo(16,
            "Cryptographic salt must be at least 128 bits (16 bytes)");

        var expectedMinLength = 13 + (int)saltSize + 32; // 13 header + salt + 256-bit subkey
        payload.Length.Should().Be(expectedMinLength,
            "Payload length must match 13 header bytes + salt length + 32-byte subkey");
    }

    [Fact]
    public void PasswordHasher_ShouldVerifyPassword_Successfully()
    {
        // Arrange
        using var provider = CreateServiceProvider();
        var hasherOptions = provider.GetRequiredService<IOptions<PasswordHasherOptions>>();
        var hasher = new PasswordHasher<ApplicationUser>(hasherOptions);
        var user = new ApplicationUser { UserName = "verify_user", Email = "verify@example.com" };

        var samplePassword = "P@ssword123Secure!";
        var hashedPassword = hasher.HashPassword(user, samplePassword);

        // Act & Assert
        var validResult = hasher.VerifyHashedPassword(user, hashedPassword, samplePassword);
        validResult.Should().Be(PasswordVerificationResult.Success,
            "Valid password must be verified with Success result");

        var invalidResult = hasher.VerifyHashedPassword(user, hashedPassword, "WrongP@ssword999!");
        invalidResult.Should().Be(PasswordVerificationResult.Failed,
            "Invalid password must fail verification");
    }

    [Fact]
    public void ApplicationUser_MustNotPersistOrExposePlaintextPassword()
    {
        // Arrange
        var userType = typeof(ApplicationUser);
        var properties = userType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Assert
        properties.Should().Contain(p => p.Name == "PasswordHash",
            "ApplicationUser must use PasswordHash for secure credential persistence");

        properties.Should().NotContain(p => p.Name.Equals("Password", StringComparison.OrdinalIgnoreCase),
            "ApplicationUser must NEVER have a plaintext 'Password' property");

        properties.Should().NotContain(p => p.Name.Contains("Plain", StringComparison.OrdinalIgnoreCase),
            "ApplicationUser must not expose any plaintext password properties");

        // Verify base class is IdentityUser<string>
        userType.IsSubclassOf(typeof(IdentityUser<string>)).Should().BeTrue(
            "ApplicationUser must inherit IdentityUser<string> which stores only PasswordHash");
    }
}

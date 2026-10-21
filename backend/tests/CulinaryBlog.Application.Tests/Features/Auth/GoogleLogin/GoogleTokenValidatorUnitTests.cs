using CulinaryBlog.Infrastructure.Authentication;
using FluentAssertions;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.GoogleLogin;

public class GoogleTokenValidatorUnitTests
{
    // ==========================================
    // 1. Direct Payload & EmailVerified Unit Tests
    // ==========================================

    [Fact]
    public void ValidatePayload_WhenPayloadIsNull_ReturnsNull()
    {
        // Act
        var result = GoogleTokenValidator.ValidatePayload(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ValidatePayload_WhenEmailVerifiedIsFalse_ReturnsNull_RejectsUnverifiedEmail()
    {
        // Arrange: payload with valid email and subject, but EmailVerified is false
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google_sub_1092837465",
            Email = "unverified_chef@example.com",
            EmailVerified = false,
            Name = "Unverified Chef"
        };

        // Act
        var result = GoogleTokenValidator.ValidatePayload(payload);

        // Assert - must reject unverified email to prevent account hijacking
        result.Should().BeNull();
    }

    [Fact]
    public void ValidatePayload_WhenEmailVerifiedIsTrue_AndValidData_ReturnsGoogleTokenInfo()
    {
        // Arrange: payload with valid verified email and subject
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google_sub_1092837465",
            Email = "verified_chef@gmail.com",
            EmailVerified = true,
            Name = "Verified Chef",
            Picture = "https://lh3.googleusercontent.com/avatar/chef.jpg"
        };

        // Act
        var result = GoogleTokenValidator.ValidatePayload(payload);

        // Assert
        result.Should().NotBeNull();
        result!.Subject.Should().Be("google_sub_1092837465");
        result.Email.Should().Be("verified_chef@gmail.com");
        result.Name.Should().Be("Verified Chef");
        result.Picture.Should().Be("https://lh3.googleusercontent.com/avatar/chef.jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePayload_WhenSubjectIsNullOrWhiteSpace_ReturnsNull(string? subject)
    {
        // Arrange
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = subject!,
            Email = "verified_chef@gmail.com",
            EmailVerified = true,
            Name = "Chef"
        };

        // Act
        var result = GoogleTokenValidator.ValidatePayload(payload);

        // Assert - subject claim is mandatory
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePayload_WhenEmailIsNullOrWhiteSpace_ReturnsNull(string? email)
    {
        // Arrange
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google_sub_12345",
            Email = email!,
            EmailVerified = true,
            Name = "Chef"
        };

        // Act
        var result = GoogleTokenValidator.ValidatePayload(payload);

        // Assert - email claim is mandatory
        result.Should().BeNull();
    }

    [Fact]
    public void ValidatePayload_WhenNameIsMissing_FallsBackToEmail()
    {
        // Arrange: Name is null, fallback to email
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google_sub_12345",
            Email = "chef_without_name@gmail.com",
            EmailVerified = true,
            Name = null
        };

        // Act
        var result = GoogleTokenValidator.ValidatePayload(payload);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("chef_without_name@gmail.com");
    }

    [Fact]
    public void ValidatePayload_WhenPictureIsProvided_PreservesPictureUrl()
    {
        // Arrange
        var payload = new GoogleJsonWebSignature.Payload
        {
            Subject = "google_sub_12345",
            Email = "chef@gmail.com",
            EmailVerified = true,
            Picture = "https://lh3.googleusercontent.com/a/photo.png"
        };

        // Act
        var result = GoogleTokenValidator.ValidatePayload(payload);

        // Assert
        result.Should().NotBeNull();
        result!.Picture.Should().Be("https://lh3.googleusercontent.com/a/photo.png");
    }

    // ==========================================
    // 2. Audience Validation Settings Unit Tests
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildValidationSettings_WhenClientIdIsNullOrWhiteSpace_ReturnsNull_FailClosed(string? clientId)
    {
        // Act
        var settings = GoogleTokenValidator.BuildValidationSettings(clientId);

        // Assert - must fail closed when client ID is missing
        settings.Should().BeNull();
    }

    [Fact]
    public void BuildValidationSettings_WhenValidClientId_SetsExactAudienceWithoutWildcard()
    {
        // Arrange
        var clientId = "  1234567890-test.apps.googleusercontent.com  ";

        // Act
        var settings = GoogleTokenValidator.BuildValidationSettings(clientId);

        // Assert
        settings.Should().NotBeNull();
        settings!.Audience.Should().NotBeNull();
        settings.Audience.Should().ContainSingle()
            .Which.Should().Be("1234567890-test.apps.googleusercontent.com");
        settings.Audience.Should().NotContain("*");
    }

    // ==========================================
    // 3. End-to-End ValidateAsync Method Tests
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WhenIdTokenIsNullOrWhiteSpace_ReturnsNull(string? idToken)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "test-client-id-12345.apps.googleusercontent.com"
            })
            .Build();

        var validator = new GoogleTokenValidator(config);

        // Act
        var result = await validator.ValidateAsync(idToken!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WhenClientIdConfigIsMissing_ReturnsNull_FailClosed()
    {
        // Arrange: no Google:ClientId in configuration
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var validator = new GoogleTokenValidator(config);

        // Act
        var result = await validator.ValidateAsync("any.mock.jwt.token");

        // Assert - must fail closed and refuse to validate without configured audience
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_WhenClientIdConfigIsEmptyOrWhitespace_ReturnsNull_FailClosed(string emptyClientId)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = emptyClientId
            })
            .Build();

        var validator = new GoogleTokenValidator(config);

        // Act
        var result = await validator.ValidateAsync("any.mock.jwt.token");

        // Assert - must fail closed
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenIsMalformedOrFake_ReturnsNull()
    {
        // Arrange: valid client id configured, but token has invalid JWT structure / signature
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Google:ClientId"] = "test-client-id-12345.apps.googleusercontent.com"
            })
            .Build();

        var validator = new GoogleTokenValidator(config);

        // Act
        var result = await validator.ValidateAsync("malformed.fake.token");

        // Assert - Google signature verification throws exception which is caught safely
        result.Should().BeNull();
    }
}

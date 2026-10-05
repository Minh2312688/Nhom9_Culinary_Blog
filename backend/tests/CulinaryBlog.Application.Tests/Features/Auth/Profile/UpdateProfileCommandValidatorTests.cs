using CulinaryBlog.Application.Features.Auth.Profile;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Profile;

public class UpdateProfileCommandValidatorTests
{
    private readonly UpdateProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidDisplayName_ShouldPass()
    {
        // Arrange
        var command = new UpdateProfileCommand("user-1", "Chef Master Nam", null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidAvatarUrl_ShouldPass()
    {
        // Arrange
        var command = new UpdateProfileCommand("user-1", null, "https://example.com/avatar.png", null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidBio_ShouldPass()
    {
        // Arrange
        var command = new UpdateProfileCommand("user-1", null, null, "Passionate about culinary traditions.");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyRequest_AllFieldsNull_ShouldFail()
    {
        // Arrange
        var command = new UpdateProfileCommand("user-1", null, null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one field"));
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://example.com/image.jpg")]
    [InlineData("javascript:alert(1)")]
    public void Validate_InvalidAvatarUrl_ShouldFail(string invalidUrl)
    {
        // Arrange
        var command = new UpdateProfileCommand("user-1", null, invalidUrl, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("AvatarUrl"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")] // length < 2
    public void Validate_DisplayNameTooShortOrEmpty_ShouldFail(string shortName)
    {
        // Arrange
        var command = new UpdateProfileCommand("user-1", shortName, null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("DisplayName"));
    }

    [Fact]
    public void Validate_DisplayNameTooLong_ShouldFail()
    {
        // Arrange: 101 characters
        var longName = new string('x', 101);
        var command = new UpdateProfileCommand("user-1", longName, null, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("DisplayName"));
    }

    [Fact]
    public void Validate_BioTooLong_ShouldFail()
    {
        // Arrange: 501 characters
        var longBio = new string('b', 501);
        var command = new UpdateProfileCommand("user-1", null, null, longBio);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Bio"));
    }
}

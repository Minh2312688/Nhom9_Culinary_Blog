using CulinaryBlog.Application.Features.Auth.Register;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = new RegisterCommand("chef@example.com", "P@ssword123", "Chef John");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("@missinguser.com")]
    public void Validate_InvalidEmail_ShouldHaveValidationError(string email)
    {
        var command = new RegisterCommand(email, "P@ssword123", "Chef John");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Email));
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("short1!")] // < 8 characters
    [InlineData("password123!")] // No uppercase
    [InlineData("PASSWORD123!")] // No lowercase
    [InlineData("Password!!!!")] // No digit
    [InlineData("Password1234")] // No special character
    public void Validate_WeakPassword_ShouldHaveValidationError(string password)
    {
        var command = new RegisterCommand("chef@example.com", password, "Chef John");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingDisplayName_ShouldHaveValidationError(string displayName)
    {
        var command = new RegisterCommand("chef@example.com", "P@ssword123", displayName);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.DisplayName));
    }

    [Fact]
    public void Validate_DisplayNameExceedsMaxLength_ShouldHaveValidationError()
    {
        var command = new RegisterCommand("chef@example.com", "P@ssword123", new string('a', 101));
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.DisplayName));
    }
}

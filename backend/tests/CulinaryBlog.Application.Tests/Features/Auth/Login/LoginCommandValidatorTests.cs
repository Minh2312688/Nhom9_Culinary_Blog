using CulinaryBlog.Application.Features.Auth.Login;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = new LoginCommand("user@example.com", "Password123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_ShouldHaveValidationError(string email)
    {
        var command = new LoginCommand(email, "Password123!");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Email));
    }

    [Fact]
    public void Validate_EmptyPassword_ShouldHaveValidationError()
    {
        var command = new LoginCommand("user@example.com", "");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Password));
    }
}

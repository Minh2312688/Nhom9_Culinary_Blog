using CulinaryBlog.Application.Features.Auth.Logout;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Logout;

public class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = new LogoutCommand("valid_token", "user-123");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "user-123", "RefreshToken")]
    [InlineData("valid_token", "", "CurrentUserId")]
    public void Validate_EmptyFields_ShouldHaveErrors(string token, string userId, string expectedField)
    {
        var command = new LogoutCommand(token, userId);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == expectedField);
    }
}

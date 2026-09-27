using CulinaryBlog.Application.Features.Auth.TokenRefresh;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.TokenRefresh;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveValidationError()
    {
        var command = new RefreshTokenCommand("valid_token_value_abc");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyToken_ShouldHaveValidationError(string? token)
    {
        var command = new RefreshTokenCommand(token!);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "RefreshToken");
    }
}

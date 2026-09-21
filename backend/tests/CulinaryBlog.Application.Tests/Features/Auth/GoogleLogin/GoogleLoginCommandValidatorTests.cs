using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.GoogleLogin;

public class GoogleLoginCommandValidatorTests
{
    private readonly GoogleLoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        var command = new GoogleLoginCommand("valid.id.token");
        var result = _validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyIdToken_ShouldHaveValidationError(string idToken)
    {
        var command = new GoogleLoginCommand(idToken);
        var result = _validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.IdToken));
    }
}

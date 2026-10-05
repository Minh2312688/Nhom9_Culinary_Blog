using CulinaryBlog.Application.Features.Auth.Profile;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Auth.Profile;

public class GetCurrentUserQueryValidatorTests
{
    private readonly GetCurrentUserQueryValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_ShouldNotHaveErrors()
    {
        var query = new GetCurrentUserQuery("user-123");
        var result = _validator.Validate(query);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_EmptyUserId_ShouldHaveError(string? userId)
    {
        var query = new GetCurrentUserQuery(userId!);
        var result = _validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }
}

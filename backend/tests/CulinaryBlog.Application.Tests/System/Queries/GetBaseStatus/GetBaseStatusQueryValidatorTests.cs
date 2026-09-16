using CulinaryBlog.Application.System.Queries.GetBaseStatus;
using Xunit;

namespace CulinaryBlog.Application.Tests.System.Queries.GetBaseStatus;

public sealed class GetBaseStatusQueryValidatorTests
{
    private readonly GetBaseStatusQueryValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_ShouldHaveError_WhenEnvironmentIsEmptyOrWhitespace(string? environment)
    {
        // Arrange
        var query = new GetBaseStatusQuery(environment!);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetBaseStatusQuery.Environment));
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenEnvironmentIsValid()
    {
        // Arrange
        var query = new GetBaseStatusQuery("Development");

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}

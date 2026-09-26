using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategoryBySlug;

public class GetCategoryBySlugQueryValidatorTests
{
    private readonly GetCategoryBySlugQueryValidator _validator = new();

    [Theory]
    [InlineData("banh-ngot")]
    [InlineData("do-an-vat")]
    public void Validate_ValidSlug_ShouldNotHaveErrors(string slug)
    {
        // Act
        var result = _validator.Validate(new GetCategoryBySlugQuery(slug));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingSlug_ShouldHaveError(string? slug)
    {
        // Act
        var result = _validator.Validate(new GetCategoryBySlugQuery(slug!));

        // Assert: lỗi => HTTP 400 Problem Details
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCategoryBySlugQuery.Slug));
    }

    [Fact]
    public void Validate_SlugLongerThanColumnLength_ShouldHaveError()
    {
        // Arrange: Category.Slug max 120 ký tự trong CategoryConfiguration
        var slug = new string('a', 121);

        // Act
        var result = _validator.Validate(new GetCategoryBySlugQuery(slug));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCategoryBySlugQuery.Slug));
    }
}
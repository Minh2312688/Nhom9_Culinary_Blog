using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategories;

public class GetCategoriesQueryValidatorTests
{
    private readonly GetCategoriesQueryValidator _validator = new();

    [Fact]
    public void Validate_DefaultQuery_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetCategoriesQuery();

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
        query.Page.Should().Be(1);
        query.PageSize.Should().Be(GetCategoriesQuery.DefaultPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_PageLessThanOne_ShouldHaveError(int page)
    {
        // Arrange
        var query = new GetCategoriesQuery(Page: page);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCategoriesQuery.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_PageSizeLessThanOne_ShouldHaveError(int pageSize)
    {
        // Arrange
        var query = new GetCategoriesQuery(PageSize: pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCategoriesQuery.PageSize));
    }

    [Fact]
    public void Validate_PageSizeOverMaximum_ShouldHaveError()
    {
        // Arrange
        var query = new GetCategoriesQuery(PageSize: GetCategoriesQuery.MaxPageSize + 1);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCategoriesQuery.PageSize));
    }

    [Fact]
    public void Validate_PageSizeAtMaximum_ShouldNotHaveErrors()
    {
        // Arrange
        var query = new GetCategoriesQuery(PageSize: GetCategoriesQuery.MaxPageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

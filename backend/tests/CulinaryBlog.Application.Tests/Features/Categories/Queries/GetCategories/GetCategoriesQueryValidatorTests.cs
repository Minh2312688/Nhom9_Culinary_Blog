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

    [Fact]
    public void Validate_DefaultQuery_ShouldUseAscendingSortOrder()
    {
        // Arrange
        var query = new GetCategoriesQuery();

        // Act & Assert
        query.SortOrder.Should().Be("asc");
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("ASC")]
    [InlineData("desc")]
    [InlineData("Desc")]
    public void Validate_SortOrderAscOrDesc_ShouldNotHaveErrors(string sortOrder)
    {
        // Arrange: CONFLICT-003 chốt sortOrder chỉ nhận asc|desc (không phân biệt hoa thường)
        var query = new GetCategoriesQuery(SortOrder: sortOrder);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ascending")]
    [InlineData("up")]
    public void Validate_SortOrderOutsideContract_ShouldHaveError(string sortOrder)
    {
        // Arrange
        var query = new GetCategoriesQuery(SortOrder: sortOrder);

        // Act
        var result = _validator.Validate(query);

        // Assert: lỗi validation => HTTP 400 Problem Details
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(GetCategoriesQuery.SortOrder));
    }
}

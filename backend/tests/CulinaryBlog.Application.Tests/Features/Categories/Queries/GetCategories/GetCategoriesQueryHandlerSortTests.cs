using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategories;

public class GetCategoriesQueryHandlerSortTests
{
    [Theory]
    [InlineData(false, "Bánh Ngọt", "Món Chay")]
    [InlineData(true, "Món Chay", "Bánh Ngọt")]
    public async Task Handle_WithSortByName_ShouldOrderItems(
        bool descending,
        string expectedFirst,
        string expectedLast)
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetCategoriesQuery(SortBy: "name", Descending: descending),
            CancellationToken.None);

        // Assert
        result.Items[0].Name.Should().Be(expectedFirst);
        result.Items[^1].Name.Should().Be(expectedLast);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WithSortByCreatedAt_ShouldOrderByCreatedAt(bool descending)
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetCategoriesQuery(SortBy: "createdAt", Descending: descending),
            CancellationToken.None);

        // Assert
        var createdAtValues = result.Items.Select(item => item.CreatedAt).ToList();
        if (descending)
        {
            createdAtValues.Should().BeInDescendingOrder();
        }
        else
        {
            createdAtValues.Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public async Task Handle_WithUnknownSortBy_ShouldFallbackToNameAscending()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetCategoriesQuery(SortBy: "unknown-field"),
            CancellationToken.None);

        // Assert
        result.Items.Select(item => item.Name).Should().BeInAscendingOrder();
        result.Items.Should().HaveCount(3);
    }
}

using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategories;

public class GetCategoriesQueryHandlerTests
{
    [Fact]
    public async Task Handle_DefaultQuery_ShouldReturnPaginatedResult()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(GetCategoriesQuery.DefaultPageSize);
        result.TotalPages.Should().Be(1);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();
        result.Items.Select(item => item.Slug)
            .Should().BeEquivalentTo(["banh-ngot", "ca-phe-sua-da", "mon-chay"]);
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnRequestedPage()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var firstPage = await handler.Handle(new GetCategoriesQuery(Page: 1, PageSize: 2), CancellationToken.None);
        var secondPage = await handler.Handle(new GetCategoriesQuery(Page: 2, PageSize: 2), CancellationToken.None);
        var pageBeyondEnd = await handler.Handle(new GetCategoriesQuery(Page: 4, PageSize: 2), CancellationToken.None);

        // Assert
        firstPage.Items.Should().HaveCount(2);
        firstPage.TotalCount.Should().Be(3);
        firstPage.TotalPages.Should().Be(2);
        firstPage.HasNextPage.Should().BeTrue();
        firstPage.HasPreviousPage.Should().BeFalse();

        secondPage.Items.Should().HaveCount(1);
        secondPage.HasNextPage.Should().BeFalse();
        secondPage.HasPreviousPage.Should().BeTrue();

        pageBeyondEnd.Items.Should().BeEmpty();
        pageBeyondEnd.TotalCount.Should().Be(3);
    }

    [Theory]
    [InlineData("chay")]
    [InlineData("CHAY")]
    [InlineData("  chay  ")]
    public async Task Handle_WithSearch_ShouldMatchNameIgnoringCaseAndWhitespace(string search)
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(Search: search), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Slug.Should().Be("mon-chay");
    }

    [Fact]
    public async Task Handle_WithSearch_ShouldMatchDescription()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetCategoriesQuery(Search: "thanh đạm"),
            CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Slug.Should().Be("mon-chay");
    }

    [Fact]
    public async Task Handle_WithSearchWithoutMatch_ShouldReturnEmptyPage()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context);

        // Act
        var result = await handler.Handle(
            new GetCategoriesQuery(Search: "không tồn tại"),
            CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }
}

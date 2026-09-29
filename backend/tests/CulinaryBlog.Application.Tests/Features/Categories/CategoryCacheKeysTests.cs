using CulinaryBlog.Application.Features.Categories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories;

/// <summary>
/// Cache key phải bao gồm ĐẦY ĐỦ tham số query ảnh hưởng kết quả, nếu không sẽ trả
/// nhầm dữ liệu giữa các lần gọi có page/search/sort khác nhau.
/// </summary>
public class CategoryCacheKeysTests
{
    [Fact]
    public void Ttl_ShouldBeThirtyMinutes()
    {
        // Contract nhóm đã chốt: Category cache TTL 30 phút.
        CategoryCacheKeys.Ttl.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void ForList_SameQuery_ShouldProduceSameKey()
    {
        // Arrange
        var first = CategoryCacheKeys.ForList(new GetCategoriesQuery(Page: 2, PageSize: 5));
        var second = CategoryCacheKeys.ForList(new GetCategoriesQuery(Page: 2, PageSize: 5));

        // Assert
        second.Should().Be(first);
    }

    [Theory]
    [InlineData(1, 10, null, "name", "asc", 2, 10, null, "name", "asc")]        // page
    [InlineData(1, 10, null, "name", "asc", 1, 20, null, "name", "asc")]        // pageSize
    [InlineData(1, 10, null, "name", "asc", 1, 10, "chay", "name", "asc")]      // search
    [InlineData(1, 10, null, "name", "asc", 1, 10, null, "createdAt", "asc")]    // sortBy
    [InlineData(1, 10, null, "name", "asc", 1, 10, null, "name", "desc")]       // sortOrder
    public void ForList_AnyDifferentQueryParameter_ShouldProduceDifferentKey(
        int pageA,
        int pageSizeA,
        string? searchA,
        string sortByA,
        string sortOrderA,
        int pageB,
        int pageSizeB,
        string? searchB,
        string sortByB,
        string sortOrderB)
    {
        // Arrange
        var first = CategoryCacheKeys.ForList(new GetCategoriesQuery(
            pageA, pageSizeA, searchA, sortByA, sortOrderA));
        var second = CategoryCacheKeys.ForList(new GetCategoriesQuery(
            pageB, pageSizeB, searchB, sortByB, sortOrderB));

        // Assert
        second.Should().NotBe(first);
    }

    [Theory]
    [InlineData("  CHAY  ", "chay")]
    [InlineData("Chay", "chay")]
    [InlineData(null, null)]
    public void ForList_SearchAndSortWithDifferentCasingOrWhitespace_ShouldShareKey(
        string? firstSearch,
        string? secondSearch)
    {
        // Arrange: cùng một filter logic thì không tạo ra nhiều key trùng lặp
        var first = CategoryCacheKeys.ForList(new GetCategoriesQuery(
            Search: firstSearch, SortBy: "Name", SortOrder: "ASC"));
        var second = CategoryCacheKeys.ForList(new GetCategoriesQuery(
            Search: secondSearch, SortBy: "name", SortOrder: "asc"));

        // Assert
        second.Should().Be(first);
    }

    [Theory]
    [InlineData("banh-ngot", "categories:slug:banh-ngot")]
    [InlineData("  BANH-NGOT  ", "categories:slug:banh-ngot")]
    public void ForSlug_ShouldNormalizeSlug(string slug, string expected)
    {
        Assert.Equal(expected, CategoryCacheKeys.ForSlug(slug));
    }
}
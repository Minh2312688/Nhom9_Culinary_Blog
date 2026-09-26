using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategories;

/// <summary>
/// Cache-aside cho GET /api/v1/categories: hit thì không chạm database, miss thì đọc
/// database rồi ghi cache với TTL 30 phút theo contract nhóm.
/// </summary>
public class GetCategoriesQueryHandlerCacheTests
{
    private static CategoryDto CachedItem(string name, string slug) =>
        new(Guid.NewGuid(), name, slug, null, null, 0, DateTime.UtcNow);

    [Fact]
    public async Task Handle_WhenCacheMiss_ShouldReadDatabaseAndCacheResultForThirtyMinutes()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var cache = new FakeCategoryCache();
        var handler = new GetCategoriesQueryHandler(context, cache);
        var query = new GetCategoriesQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalCount.Should().Be(3);
        cache.SetCalls.Should().ContainSingle();
        cache.SetCalls[0].Key.Should().Be(CategoryCacheKeys.ForList(query));
        cache.SetCalls[0].Ttl.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Handle_WhenCacheHit_ShouldReturnCachedResultWithoutReadingDatabase()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var cache = new FakeCategoryCache();
        var handler = new GetCategoriesQueryHandler(context, cache);
        var query = new GetCategoriesQuery(PageSize: 2);
        var cachedResult = new PaginatedResult<CategoryDto>(
            [CachedItem("Bánh Ngọt", "banh-ngot")], 1, 1, 2);
        cache.Seed(CategoryCacheKeys.ForList(query), cachedResult);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert: trả đúng dữ liệu cache và không ghi lại cache
        result.Should().BeSameAs(cachedResult);
        cache.SetCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SecondCallWithSameQuery_ShouldBeServedFromCache()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var cache = new FakeCategoryCache();
        var handler = new GetCategoriesQueryHandler(context, cache);
        var query = new GetCategoriesQuery();

        // Act: lần 1 cache miss, lần 2 phải hit
        var first = await handler.Handle(query, CancellationToken.None);
        var second = await handler.Handle(query, CancellationToken.None);

        // Assert
        first.TotalCount.Should().Be(3);
        second.TotalCount.Should().Be(3);
        cache.GetCount.Should().Be(2);
        cache.SetCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DifferentQueryParameters_ShouldNotReuseOtherCacheEntry()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var cache = new FakeCategoryCache();
        var handler = new GetCategoriesQueryHandler(context, cache);

        // Act
        await handler.Handle(new GetCategoriesQuery(Search: "chay"), CancellationToken.None);
        await handler.Handle(new GetCategoriesQuery(Search: "ngọt"), CancellationToken.None);

        // Assert: mỗi query có key riêng nên đều phải đọc database
        cache.SetCalls.Select(call => call.Key).Should().OnlyHaveUniqueItems();
        cache.SetCalls.Should().HaveCount(2);
    }
}
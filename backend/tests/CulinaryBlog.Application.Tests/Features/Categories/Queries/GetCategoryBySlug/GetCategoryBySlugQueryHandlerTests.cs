using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategoryBySlug;

/// <summary>
/// GET /api/v1/categories/{slug} (FR-CAT-002): trả category theo slug, 404 theo exception
/// convention khi không tồn tại hoặc đã soft delete, và cache-aside TTL 30 phút.
/// </summary>
public class GetCategoryBySlugQueryHandlerTests
{
    private static async Task<(CategoryTestDbContext Context, Category Category)> SeedAsync()
    {
        var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt", "Mô tả món bánh");
        category.ImageUrl = "https://cdn.example.test/banh-ngot.jpg";
        category.OrderIndex = 3;
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return (context, category);
    }

    [Fact]
    public async Task Handle_ExistingSlug_ShouldReturnCategory()
    {
        // Arrange
        var (context, category) = await SeedAsync();
        await using var _ = context;
        var handler = new GetCategoryBySlugQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(
            new GetCategoryBySlugQuery("banh-ngot"),
            CancellationToken.None);

        // Assert
        result.Id.Should().Be(category.Id);
        result.Name.Should().Be("Bánh Ngọt");
        result.Slug.Should().Be("banh-ngot");
        result.Description.Should().Be("Mô tả món bánh");
        result.ImageUrl.Should().Be("https://cdn.example.test/banh-ngot.jpg");
        result.OrderIndex.Should().Be(3);
    }

    [Fact]
    public async Task Handle_UppercaseSlug_ShouldReturnSameCategoryAsLowercase()
    {
        // Arrange: slug lưu trong database luôn lowercase, nên request viết hoa phải
        // cho kết quả giống hệt slug thường, kể cả khi cache đang miss.
        var (context, category) = await SeedAsync();
        await using var _ = context;
        var handler = new GetCategoryBySlugQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(
            new GetCategoryBySlugQuery("BANH-NGOT"),
            CancellationToken.None);

        // Assert
        result.Id.Should().Be(category.Id);
        result.Slug.Should().Be("banh-ngot");
    }

    [Fact]
    public async Task Handle_SlugWithSurroundingWhitespace_ShouldReturnCategory()
    {
        // Arrange
        var (context, category) = await SeedAsync();
        await using var _ = context;
        var handler = new GetCategoryBySlugQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(
            new GetCategoryBySlugQuery("  banh-ngot  "),
            CancellationToken.None);

        // Assert
        result.Id.Should().Be(category.Id);
    }

    [Fact]
    public async Task Handle_CacheHitAndCacheMiss_ShouldBehaveConsistentlyAcrossSlugCasing()
    {
        // Arrange: cache key và truy vấn EF phải dùng cùng một dạng slug đã chuẩn hoá,
        // nếu không thì cache hit và cache miss sẽ trả kết quả khác nhau.
        var (context, category) = await SeedAsync();
        await using var _ = context;
        var cache = new FakeCategoryCache();
        var handler = new GetCategoryBySlugQueryHandler(context, cache);

        // Act: lần 1 (slug thường) cache miss, lần 2 (slug hoa) phải hit cache
        var onCacheMiss = await handler.Handle(
            new GetCategoryBySlugQuery("banh-ngot"),
            CancellationToken.None);
        var onCacheHit = await handler.Handle(
            new GetCategoryBySlugQuery("BANH-NGOT"),
            CancellationToken.None);

        // Assert
        onCacheMiss.Id.Should().Be(category.Id);
        onCacheHit.Id.Should().Be(category.Id);
        cache.SetCalls.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownSlug_ShouldThrowNotFoundException()
    {
        // Arrange
        var context = CategoryTestDbContext.CreateInMemory();
        await using var _ = context;
        var handler = new GetCategoryBySlugQueryHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new GetCategoryBySlugQuery("khong-ton-tai"),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SoftDeletedCategory_ShouldThrowNotFoundException()
    {
        // Arrange
        var context = CategoryTestDbContext.CreateInMemory();
        await using var _ = context;
        var category = Category.Create("Bánh Ngọt");
        category.Delete();
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var handler = new GetCategoryBySlugQueryHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new GetCategoryBySlugQuery("banh-ngot"),
            CancellationToken.None);

        // Assert: row vẫn tồn tại (soft delete) nhưng không được trả về
        await act.Should().ThrowAsync<NotFoundException>();
        (await context.Categories.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_CacheMiss_ShouldCacheResultForThirtyMinutes()
    {
        // Arrange
        var (context, _) = await SeedAsync();
        await using var _ = context;
        var cache = new FakeCategoryCache();
        var handler = new GetCategoryBySlugQueryHandler(context, cache);

        // Act
        await handler.Handle(new GetCategoryBySlugQuery("banh-ngot"), CancellationToken.None);

        // Assert
        cache.SetCalls.Should().ContainSingle();
        cache.SetCalls[0].Key.Should().Be(CategoryCacheKeys.ForSlug("banh-ngot"));
        cache.SetCalls[0].Ttl.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldNotQueryDatabase()
    {
        // Arrange
        var (context, _) = await SeedAsync();
        await using var _ = context;
        var cache = new FakeCategoryCache();
        var handler = new GetCategoryBySlugQueryHandler(context, cache);
        cache.Seed(
            CategoryCacheKeys.ForSlug("banh-ngot"),
            new CulinaryBlog.Application.DTOs.CategoryDto(
                Guid.NewGuid(), "Bánh Ngọt", "banh-ngot", null, null, 0, DateTime.UtcNow));

        // Act
        var result = await handler.Handle(
            new GetCategoryBySlugQuery("banh-ngot"),
            CancellationToken.None);

        // Assert
        result.Name.Should().Be("Bánh Ngọt");
        cache.SetCalls.Should().BeEmpty();
    }
}
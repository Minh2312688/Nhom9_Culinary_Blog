using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Queries.GetCategories;

/// <summary>
/// FR-CAT-001: mỗi category trong list phải có recipeCount = số Recipe Published
/// (không tính Draft/Archived và không tính recipe đã soft delete).
/// </summary>
public class GetCategoriesQueryHandlerRecipeCountTests
{
    private static Recipe CreateRecipe(
        Category category,
        string title,
        RecipeStatus status,
        bool softDeleted = false) =>
        new()
        {
            Title = title,
            Slug = title.ToLowerInvariant().Replace(' ', '-'),
            CategoryId = category.Id,
            AuthorId = "test-author",
            Status = status,
            IsDeleted = softDeleted
        };

    [Fact]
    public async Task Handle_CategoryWithoutRecipes_ShouldReturnZeroRecipeCount()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items.Should().OnlyContain(item => item.RecipeCount == 0);
    }

    [Fact]
    public async Task Handle_PublishedRecipes_ShouldBeCounted()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var category = await context.Categories.SingleAsync(c => c.Slug == "banh-ngot");
        context.Recipes.AddRange(
            CreateRecipe(category, "Bánh Flan", RecipeStatus.Published),
            CreateRecipe(category, "Bánh Chuối", RecipeStatus.Published));
        await context.SaveChangesAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        // Assert
        result.Items.Single(item => item.Slug == "banh-ngot").RecipeCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DraftAndArchivedRecipes_ShouldNotBeCounted()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var category = await context.Categories.SingleAsync(c => c.Slug == "banh-ngot");
        context.Recipes.AddRange(
            CreateRecipe(category, "Bánh Flan", RecipeStatus.Published),
            CreateRecipe(category, "Bánh Nháp", RecipeStatus.Draft),
            CreateRecipe(category, "Bánh Cũ", RecipeStatus.Archived));
        await context.SaveChangesAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        // Assert: chỉ Published được tính
        result.Items.Single(item => item.Slug == "banh-ngot").RecipeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SoftDeletedRecipe_ShouldNotBeCounted()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var category = await context.Categories.SingleAsync(c => c.Slug == "banh-ngot");
        context.Recipes.AddRange(
            CreateRecipe(category, "Bánh Flan", RecipeStatus.Published),
            CreateRecipe(category, "Bánh Đã Xoá", RecipeStatus.Published, softDeleted: true));
        await context.SaveChangesAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        // Assert
        result.Items.Single(item => item.Slug == "banh-ngot").RecipeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MultipleCategories_ShouldReturnCountForEachCategory()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var cake = await context.Categories.SingleAsync(c => c.Slug == "banh-ngot");
        var coffee = await context.Categories.SingleAsync(c => c.Slug == "ca-phe-sua-da");
        context.Recipes.AddRange(
            CreateRecipe(cake, "Bánh Flan", RecipeStatus.Published),
            CreateRecipe(cake, "Bánh Chuối", RecipeStatus.Published),
            CreateRecipe(cake, "Bánh Nháp", RecipeStatus.Draft),
            CreateRecipe(coffee, "Cà Phê Đen", RecipeStatus.Published));
        await context.SaveChangesAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        // Assert
        result.Items.Single(item => item.Slug == "banh-ngot").RecipeCount.Should().Be(2);
        result.Items.Single(item => item.Slug == "ca-phe-sua-da").RecipeCount.Should().Be(1);
        result.Items.Single(item => item.Slug == "mon-chay").RecipeCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCountOnlyForRequestedPage()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var coffee = await context.Categories.SingleAsync(c => c.Slug == "ca-phe-sua-da");
        var vegan = await context.Categories.SingleAsync(c => c.Slug == "mon-chay");
        context.Recipes.AddRange(
            CreateRecipe(coffee, "Cà Phê Đen", RecipeStatus.Published),
            CreateRecipe(vegan, "Bánh Chay", RecipeStatus.Published));
        await context.SaveChangesAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act: sortBy=name => trang 1 là "Bánh Ngọt", trang 2 là "Cà Phê Sữa Đá"
        var firstPage = await handler.Handle(
            new GetCategoriesQuery(Page: 1, PageSize: 1, SortBy: "name"),
            CancellationToken.None);
        var secondPage = await handler.Handle(
            new GetCategoriesQuery(Page: 2, PageSize: 1, SortBy: "name"),
            CancellationToken.None);

        // Assert
        firstPage.Items[0].Slug.Should().Be("banh-ngot");
        firstPage.Items[0].RecipeCount.Should().Be(0);
        secondPage.Items[0].Slug.Should().Be("ca-phe-sua-da");
        secondPage.Items[0].RecipeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithSearch_ShouldReturnCountForMatchingCategoriesOnly()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var cake = await context.Categories.SingleAsync(c => c.Slug == "banh-ngot");
        var coffee = await context.Categories.SingleAsync(c => c.Slug == "ca-phe-sua-da");
        context.Recipes.AddRange(
            CreateRecipe(cake, "Bánh Flan", RecipeStatus.Published),
            CreateRecipe(cake, "Bánh Chuối", RecipeStatus.Published),
            CreateRecipe(coffee, "Cà Phê Đen", RecipeStatus.Published));
        await context.SaveChangesAsync();
        var handler = new GetCategoriesQueryHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(
            new GetCategoriesQuery(Search: "ngọt"),
            CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle();
        result.Items[0].Slug.Should().Be("banh-ngot");
        result.Items[0].RecipeCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CacheHit_ShouldRefreshRecipeCountFromCurrentRecipes()
    {
        // Arrange
        await using var context = await CategoriesTestData.SeedDefaultAsync();
        var category = await context.Categories.SingleAsync(c => c.Slug == "banh-ngot");
        context.Recipes.Add(CreateRecipe(category, "Bánh Flan", RecipeStatus.Published));
        await context.SaveChangesAsync();

        var request = new GetCategoriesQuery();
        var cachedResult = new PaginatedResult<CategoryDto>(
            [new CategoryDto(category.Id, category.Name, category.Slug, category.Description, null, category.OrderIndex, category.CreatedAt)
            {
                RecipeCount = 0
            }],
            1,
            1,
            request.PageSize);
        var cache = new FakeCategoryCache();
        cache.Seed(CategoryCacheKeys.ForList(request), cachedResult);
        var handler = new GetCategoriesQueryHandler(context, cache);

        // Act
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle();
        result.Items[0].RecipeCount.Should().Be(1);
        cache.SetCalls.Should().BeEmpty();
    }
}
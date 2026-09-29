using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Commands.DeleteCategory;

public class DeleteCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingCategory_ShouldDeleteCategory()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt");
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var handler = new DeleteCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        // Assert
        var storedCount = await context.Categories.CountAsync();
        storedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownCategory_ShouldThrowNotFoundException()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var handler = new DeleteCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new DeleteCategoryCommand(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_ExistingCategory_ShouldKeepRowAndMarkIsDeleted()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt");
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var cache = new FakeCategoryCache();
        var handler = new DeleteCategoryCommandHandler(context, cache);

        // Act
        await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        // Assert: soft delete (CONFLICT-002), không hard delete
        (await context.Categories.CountAsync()).Should().Be(0);
        var stored = await context.Categories.IgnoreQueryFilters().SingleAsync();
        stored.IsDeleted.Should().BeTrue();
        stored.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ExistingCategory_ShouldInvalidateCategoryCache()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt");
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var cache = new FakeCategoryCache();
        var handler = new DeleteCategoryCommandHandler(context, cache);

        // Act
        await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        // Assert
        cache.InvalidateCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AlreadySoftDeletedCategory_ShouldThrowNotFoundException()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt");
        category.Delete();
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var handler = new DeleteCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new DeleteCategoryCommand(category.Id),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CategoryWithActiveRecipe_ShouldThrowConflictException()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt");
        context.Categories.Add(category);
        context.Recipes.Add(new Recipe
        {
            Title = "Bánh Flan",
            Slug = "banh-flan",
            CategoryId = category.Id,
            AuthorId = "test-author"
        });
        await context.SaveChangesAsync();
        var cache = new FakeCategoryCache();
        var handler = new DeleteCategoryCommandHandler(context, cache);

        // Act
        var act = async () => await handler.Handle(
            new DeleteCategoryCommand(category.Id),
            CancellationToken.None);

        // Assert: 409 Conflict, category chưa bị xoá, cache chưa invalidate
        await act.Should().ThrowAsync<ConflictException>();
        (await context.Categories.IgnoreQueryFilters().SingleAsync())
            .IsDeleted.Should().BeFalse();
        cache.InvalidateCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CategoryWithOnlySoftDeletedRecipe_ShouldDeleteCategory()
    {
        // Arrange: chỉ Recipe active (theo global query filter) mới chặn xoá
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Bánh Ngọt");
        context.Categories.Add(category);
        context.Recipes.Add(new Recipe
        {
            Title = "Bánh Flan",
            Slug = "banh-flan",
            CategoryId = category.Id,
            AuthorId = "test-author",
            IsDeleted = true
        });
        await context.SaveChangesAsync();
        var handler = new DeleteCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        // Assert
        (await context.Categories.IgnoreQueryFilters().SingleAsync())
            .IsDeleted.Should().BeTrue();
    }
}

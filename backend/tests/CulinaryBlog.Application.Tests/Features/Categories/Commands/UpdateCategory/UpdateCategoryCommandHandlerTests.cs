using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Commands.UpdateCategory;

/// <summary>
/// PUT /api/v1/categories/{id} (CONFLICT-017): cập nhật Name, Description, ImageUrl, OrderIndex;
/// slug giữ nguyên; soft-deleted không sửa được; trùng tên trả ConflictException (409).
/// </summary>
public class UpdateCategoryCommandHandlerTests
{
    private static async Task<(CategoryTestDbContext Context, Category Category, FakeCategoryCache Cache)>
        SeedAsync()
    {
        var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Món Tráng Miệng", "Mô tả cũ");
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return (context, category, new FakeCategoryCache());
    }

    [Fact]
    public async Task Handle_ExistingCategory_ShouldUpdateNameDescriptionImageUrlAndOrderIndex()
    {
        // Arrange
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        await handler.Handle(
            new UpdateCategoryCommand(
                category.Id,
                "  Bánh Ngọt  ",
                "  Mô tả mới  ",
                "  https://cdn.example.test/banh-ngot.jpg  ",
                7),
            CancellationToken.None);

        // Assert
        var stored = await context.Categories.IgnoreQueryFilters().SingleAsync();
        stored.Name.Should().Be("Bánh Ngọt");
        stored.Description.Should().Be("Mô tả mới");
        stored.ImageUrl.Should().Be("https://cdn.example.test/banh-ngot.jpg");
        stored.OrderIndex.Should().Be(7);
    }

    [Fact]
    public async Task Handle_ExistingCategory_ShouldKeepSlugUnchanged()
    {
        // Arrange: quyết định nhóm - đổi tên không được làm hỏng URL cũ
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Bánh Ngọt", null, null, 0),
            CancellationToken.None);

        // Assert
        (await context.Categories.IgnoreQueryFilters().SingleAsync())
            .Slug.Should().Be("mon-trang-mieng");
    }

    [Fact]
    public async Task Handle_ExistingCategory_ShouldReturnUpdatedCategory()
    {
        // Arrange
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        var result = await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Bánh Ngọt", "Mô tả", "https://cdn.test/a.jpg", 2),
            CancellationToken.None);

        // Assert
        result.Id.Should().Be(category.Id);
        result.Name.Should().Be("Bánh Ngọt");
        result.Slug.Should().Be("mon-trang-mieng");
        result.ImageUrl.Should().Be("https://cdn.test/a.jpg");
        result.OrderIndex.Should().Be(2);
    }

    [Fact]
    public async Task Handle_UnknownCategory_ShouldThrowNotFoundException()
    {
        // Arrange
        var (context, _, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        var act = async () => await handler.Handle(
            new UpdateCategoryCommand(Guid.NewGuid(), "Bánh Ngọt", null, null, 0),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_SoftDeletedCategory_ShouldThrowNotFoundException()
    {
        // Arrange
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        category.Delete();
        await context.SaveChangesAsync();
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        var act = async () => await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Tên Mới", null, null, 0),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_DuplicateNameOnAnotherCategory_ShouldThrowConflictException()
    {
        // Arrange: slug cố định nên phải kiểm tra trùng Name ở tầng application
        var context = CategoryTestDbContext.CreateInMemory();
        await using var _ = context;
        var category = Category.Create("Món Tráng Miệng");
        context.Categories.AddRange(category, Category.Create("Bánh Ngọt"));
        await context.SaveChangesAsync();
        var handler = new UpdateCategoryCommandHandler(context, new FakeCategoryCache());

        // Act: khác hoa thường + khoảng trắng vẫn là trùng tên
        var act = async () => await handler.Handle(
            new UpdateCategoryCommand(category.Id, "  bánh NGỌT  ", null, null, 0),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_DuplicateName_ShouldNotModifyCategory()
    {
        // Arrange
        var context = CategoryTestDbContext.CreateInMemory();
        await using var _ = context;
        var category = Category.Create("Món Tráng Miệng", "Mô tả cũ");
        context.Categories.AddRange(category, Category.Create("Bánh Ngọt"));
        await context.SaveChangesAsync();
        var handler = new UpdateCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Bánh Ngọt", "Mô tả mới", null, 5),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        var stored = await context.Categories.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == category.Id);
        stored.Name.Should().Be("Món Tráng Miệng");
        stored.Description.Should().Be("Mô tả cũ");
        stored.OrderIndex.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SameNameWithDifferentCasingAndWhitespace_ShouldSucceed()
    {
        // Arrange: không được báo trùng khi chỉ khác hoa thường/khoảng trắng của chính nó
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        var result = await handler.Handle(
            new UpdateCategoryCommand(category.Id, "  món tráng miệng  ", null, null, 0),
            CancellationToken.None);

        // Assert
        result.Name.Should().Be("món tráng miệng");
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldInvalidateCategoryCache()
    {
        // Arrange
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act
        await handler.Handle(
            new UpdateCategoryCommand(category.Id, "Bánh Ngọt", null, null, 0),
            CancellationToken.None);

        // Assert
        cache.InvalidateCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_InvalidName_ShouldThrowAndNotModifyCategory()
    {
        // Arrange
        var (context, category, cache) = await SeedAsync();
        await using var _ = context;
        var handler = new UpdateCategoryCommandHandler(context, cache);

        // Act: tên 1 ký tự vi phạm domain guard
        var act = async () => await handler.Handle(
            new UpdateCategoryCommand(category.Id, "A", null, null, 9),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        var stored = await context.Categories.IgnoreQueryFilters().SingleAsync();
        stored.Name.Should().Be("Món Tráng Miệng");
        stored.OrderIndex.Should().Be(0);
        cache.InvalidateCount.Should().Be(0);
    }
}

using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_ShouldPersistCategoryWithGeneratedSlug()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var handler = new CreateCategoryCommandHandler(context, new FakeCategoryCache());
        var command = new CreateCategoryCommand("  Món Tráng Miệng  ", "  Tráng miệng truyền thống  ");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().NotBeEmpty();
        result.Name.Should().Be("Món Tráng Miệng");
        result.Slug.Should().Be("mon-trang-mieng");
        result.Description.Should().Be("Tráng miệng truyền thống");

        var stored = await context.Categories.SingleAsync();
        stored.Id.Should().Be(result.Id);
        stored.Slug.Should().Be("mon-trang-mieng");
        stored.CreatedAt.Should().Be(result.CreatedAt);
    }

    [Fact]
    public async Task Handle_CommandWithoutDescription_ShouldStoreNullDescription()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var handler = new CreateCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var result = await handler.Handle(new CreateCategoryCommand("Bánh Ngọt", "   "), CancellationToken.None);

        // Assert
        result.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CommandWithInvalidName_ShouldThrowArgumentExceptionFromDomainGuard()
    {
        // Arrange: handler chạy trực tiếp (không qua ValidationBehavior) để kiểm tra guard của Domain
        await using var context = CategoryTestDbContext.CreateInMemory();
        var handler = new CreateCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(new CreateCategoryCommand("A", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("Bánh Ngọt")]
    [InlineData("  bánh ngọt  ")]
    [InlineData("BÁNH NGỌT")]
    public async Task Handle_DuplicateSlug_ShouldThrowConflictException(string duplicateName)
    {
        // Arrange: slug sinh deterministic từ tên, nên trùng tên cũng là trùng slug
        await using var context = CategoryTestDbContext.CreateInMemory();
        context.Categories.Add(Category.Create("Bánh Ngọt"));
        await context.SaveChangesAsync();
        var handler = new CreateCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new CreateCategoryCommand(duplicateName, null),
            CancellationToken.None);

        // Assert: 409 Conflict, không tạo thêm category
        await act.Should().ThrowAsync<ConflictException>();
        (await context.Categories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_NameOfSoftDeletedCategory_ShouldThrowConflictException()
    {
        // Arrange: IX_Categories_Slug vẫn giữ row đã soft delete nên tên cũ không tái dùng được
        await using var context = CategoryTestDbContext.CreateInMemory();
        var deleted = Category.Create("Bánh Ngọt");
        deleted.Delete();
        context.Categories.Add(deleted);
        await context.SaveChangesAsync();
        var handler = new CreateCategoryCommandHandler(context, new FakeCategoryCache());

        // Act
        var act = async () => await handler.Handle(
            new CreateCategoryCommand("Bánh Ngọt", null),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_ValidCommand_ShouldInvalidateCategoryCache()
    {
        // Arrange
        await using var context = CategoryTestDbContext.CreateInMemory();
        var cache = new FakeCategoryCache();
        var handler = new CreateCategoryCommandHandler(context, cache);

        // Act
        await handler.Handle(new CreateCategoryCommand("Món Chay", null), CancellationToken.None);

        // Assert
        cache.InvalidateCount.Should().Be(1);
    }
}

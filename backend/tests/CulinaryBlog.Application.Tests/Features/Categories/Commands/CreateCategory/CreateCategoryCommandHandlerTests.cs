using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
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
        var handler = new CreateCategoryCommandHandler(context);
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
        var handler = new CreateCategoryCommandHandler(context);

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
        var handler = new CreateCategoryCommandHandler(context);

        // Act
        var act = async () => await handler.Handle(new CreateCategoryCommand("A", null), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

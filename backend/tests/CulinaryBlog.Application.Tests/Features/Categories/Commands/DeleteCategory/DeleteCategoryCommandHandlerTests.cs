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

        var handler = new DeleteCategoryCommandHandler(context);

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
        var handler = new DeleteCategoryCommandHandler(context);

        // Act
        var act = async () => await handler.Handle(
            new DeleteCategoryCommand(Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Domain.Entities;

/// <summary>
/// Unit test cho hành vi Update của Category: UpdatedAt, trim và tính toàn vẹn khi input sai.
/// </summary>
public class CategoryUpdateTests
{
    [Fact]
    public void Update_ShouldSetUpdatedAtAndKeepCreatedAt()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng");
        var createdAt = category.CreatedAt;

        // Act
        category.Update("Bánh Ngọt", "Mô tả mới");

        // Assert
        category.UpdatedAt.Should().NotBeNull();
        category.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        category.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Update_ShouldTrimNameAndDescription()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng");

        // Act
        category.Update("  Bánh Ngọt  ", "   Mô tả mới   ");

        // Assert
        category.Name.Should().Be("Bánh Ngọt");
        category.Description.Should().Be("Mô tả mới");
    }

    [Fact]
    public void Update_WithEmptyDescription_ShouldClearDescription()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng", "Mô tả cũ");

        // Act
        category.Update("Món Tráng Miệng", "   ");

        // Assert
        category.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A")]
    public void Update_WithInvalidName_ShouldThrowArgumentException(string? name)
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng");

        // Act
        var act = () => category.Update(name!, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_WithNameLongerThanMaximum_ShouldThrowArgumentException()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng");
        var name = new string('a', Category.MaxNameLength + 1);

        // Act
        var act = () => category.Update(name, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_WithInvalidDescription_ShouldNotChangeEntity()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng", "Mô tả cũ");
        var createdAt = category.CreatedAt;
        var updatedAtBefore = category.UpdatedAt;

        // Act
        var act = () => category.Update(
            "Bánh Ngọt",
            new string('a', Category.MaxDescriptionLength + 1));

        // Assert: input sai không được để entity ở trạng thái nửa vời
        act.Should().Throw<ArgumentException>();
        category.Name.Should().Be("Món Tráng Miệng");
        category.Slug.Should().Be("mon-trang-mieng");
        category.Description.Should().Be("Mô tả cũ");
        category.CreatedAt.Should().Be(createdAt);
        category.UpdatedAt.Should().Be(updatedAtBefore);
    }
}

using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Domain.Entities;

/// <summary>
/// Unit test cho validation domain của Category: trim, độ dài Name/Description
/// và hành vi Update (UpdatedAt, không thay đổi CreatedAt).
/// </summary>
public class CategoryTests
{
    [Fact]
    public void Create_WithSurroundingWhitespace_ShouldTrimNameAndDescription()
    {
        // Act
        var category = Category.Create("  Bánh Ngọt  ", "  Mô tả món bánh  ");

        // Assert
        category.Name.Should().Be("Bánh Ngọt");
        category.Description.Should().Be("Mô tả món bánh");
    }

    [Fact]
    public void Create_WithoutDescription_ShouldStoreNullDescription()
    {
        // Act
        var category = Category.Create("Bánh Ngọt");

        // Assert
        category.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Create_WithWhitespaceDescription_ShouldStoreNullDescription(string description)
    {
        // Act
        var category = Category.Create("Bánh Ngọt", description);

        // Assert
        category.Description.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_ShouldThrowArgumentException(string? name)
    {
        // Act
        var act = () => Category.Create(name!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("A")]
    [InlineData(" A ")]
    public void Create_WithNameShorterThanMinimum_ShouldThrowArgumentException(string name)
    {
        // Act
        var act = () => Category.Create(name);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameAtMinimumLength_ShouldSucceed()
    {
        // Act
        var category = Category.Create("Ab");

        // Assert
        category.Name.Should().Be("Ab");
        category.Slug.Should().Be("ab");
    }

    [Fact]
    public void Create_WithNameLongerThanMaximum_ShouldThrowArgumentException()
    {
        // Arrange
        var name = new string('a', Category.MaxNameLength + 1);

        // Act
        var act = () => Category.Create(name);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithDescriptionAtMaximumLength_ShouldSucceed()
    {
        // Arrange
        var description = new string('a', Category.MaxDescriptionLength);

        // Act
        var category = Category.Create("Bánh Ngọt", description);

        // Assert
        category.Description.Should().HaveLength(Category.MaxDescriptionLength);
    }

    [Fact]
    public void Create_WithDescriptionLongerThanMaximum_ShouldThrowArgumentException()
    {
        // Arrange
        var description = new string('a', Category.MaxDescriptionLength + 1);

        // Act
        var act = () => Category.Create("Bánh Ngọt", description);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldInitializeCreatedAtAndLeaveUpdatedAtNull()
    {
        // Act
        var category = Category.Create("Bánh Ngọt");

        // Assert
        category.Id.Should().NotBeEmpty();
        category.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        category.UpdatedAt.Should().BeNull();
    }
}

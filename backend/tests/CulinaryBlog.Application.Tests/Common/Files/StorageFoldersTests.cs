using CulinaryBlog.Application.Contracts.Storage;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common.Files;

/// <summary>
/// Unit test cho StorageFolders: folder phải an toàn trước khi ghép vào object name.
/// </summary>
public class StorageFoldersTests
{
    [Fact]
    public void Categories_ShouldBeValidLowercaseFolder()
    {
        StorageFolders.Categories.Should().Be("categories");
        StorageFolders.IsValid(StorageFolders.Categories).Should().BeTrue();
    }

    [Fact]
    public void ForRecipe_ShouldReturnNestedFolderWithCompactGuid()
    {
        // Arrange
        var recipeId = Guid.Parse("2f1a4c3e-5b6d-4e7f-8a9b-0c1d2e3f4a5b");

        // Act
        var folder = StorageFolders.ForRecipe(recipeId);

        // Assert
        folder.Should().Be("recipes/2f1a4c3e5b6d4e7f8a9b0c1d2e3f4a5b");
        StorageFolders.IsValid(folder).Should().BeTrue();
    }

    [Fact]
    public void ForRecipe_WithEmptyGuid_ShouldThrowArgumentException()
    {
        // Act
        var act = () => StorageFolders.ForRecipe(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("categories")]
    [InlineData("recipes/2f1a4c3e5b6d4e7f8a9b0c1d2e3f4a5b")]
    [InlineData("recipes/abc-123")]
    public void IsValid_ValidFolder_ShouldBeTrue(string folder)
    {
        StorageFolders.IsValid(folder).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Categories")]
    [InlineData("/categories")]
    [InlineData("categories/")]
    [InlineData("categories\\sub")]
    [InlineData("categories/..")]
    [InlineData("../categories")]
    [InlineData("cate gories")]
    [InlineData("categories//sub")]
    public void IsValid_InvalidFolder_ShouldBeFalse(string? folder)
    {
        StorageFolders.IsValid(folder).Should().BeFalse();
    }

    [Fact]
    public void IsValid_FolderLongerThanMaximumLength_ShouldBeFalse()
    {
        // Arrange
        var folder = new string('a', StorageFolders.MaxLength + 1);

        // Act & Assert
        StorageFolders.IsValid(folder).Should().BeFalse();
    }
}

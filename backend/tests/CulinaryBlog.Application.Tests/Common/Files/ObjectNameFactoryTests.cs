using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common.Files;

/// <summary>
/// Unit test cho ObjectNameFactory: object name phải unique, an toàn
/// và không chứa bất kỳ text do client cung cấp.
/// </summary>
public class ObjectNameFactoryTests
{
    private const string ObjectNamePattern = "^[a-z0-9-]+(/[a-z0-9-]+)*/[0-9a-f]{32}[.](jpg|png|webp|avif)$";

    [Fact]
    public void Create_WithCategoriesFolder_ShouldReturnGuidObjectNameInFolder()
    {
        // Act
        var objectName = ObjectNameFactory.Create(StorageFolders.Categories, ImageFileFormat.Png);

        // Assert
        Regex.IsMatch(objectName, ObjectNamePattern).Should().BeTrue(objectName);
        objectName.Should().StartWith("categories/");
        objectName.Should().EndWith(".png");
    }

    [Fact]
    public void Create_WithRecipeFolder_ShouldReturnNestedFolderAndGuidObjectName()
    {
        // Arrange
        var folder = StorageFolders.ForRecipe(Guid.NewGuid());

        // Act
        var objectName = ObjectNameFactory.Create(folder, ImageFileFormat.Jpeg);

        // Assert
        Regex.IsMatch(objectName, ObjectNamePattern).Should().BeTrue(objectName);
        objectName.Should().StartWith(folder + "/");
        objectName.Should().EndWith(".jpg");
    }

    [Fact]
    public void Create_ShouldGenerateUniqueObjectNames()
    {
        // Act
        var objectNames = Enumerable
            .Range(0, 2000)
            .Select(_ => ObjectNameFactory.Create(StorageFolders.Categories, ImageFileFormat.Jpeg))
            .ToList();

        // Assert
        objectNames.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(ImageFileFormat.Jpeg, ".jpg")]
    [InlineData(ImageFileFormat.Png, ".png")]
    [InlineData(ImageFileFormat.WebP, ".webp")]
    [InlineData(ImageFileFormat.Avif, ".avif")]
    public void Create_ShouldUseCanonicalExtensionFromDetectedFormat(ImageFileFormat format, string expectedExtension)
    {
        // Act
        var objectName = ObjectNameFactory.Create(StorageFolders.Categories, format);

        // Assert
        objectName.Should().EndWith(expectedExtension);
    }

    [Theory]
    [InlineData("Categories")]
    [InlineData("/categories")]
    [InlineData("categories/")]
    [InlineData("recipes\\123")]
    [InlineData("categories/..")]
    [InlineData("../categories")]
    [InlineData("cate gories")]
    [InlineData("categories//sub")]
    public void Create_WithInvalidFolder_ShouldThrowArgumentException(string folder)
    {
        // Act
        var act = () => ObjectNameFactory.Create(folder, ImageFileFormat.Jpeg);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithNullOrWhitespaceFolder_ShouldThrowArgumentException(string? folder)
    {
        // Act
        var act = () => ObjectNameFactory.Create(folder!, ImageFileFormat.Jpeg);

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}

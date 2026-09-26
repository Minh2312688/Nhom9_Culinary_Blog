using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Domain.Entities;

/// <summary>
/// Unit test cho sinh slug không dấu của Category (bao gồm tiếng Việt có dấu).
/// </summary>
public class CategorySlugTests
{
    [Theory]
    [InlineData("Món Tráng Miệng", "mon-trang-mieng")]
    [InlineData("Đồ Ăn Vặt", "do-an-vat")]
    [InlineData("Cà Phê Sữa Đá", "ca-phe-sua-da")]
    [InlineData("Bánh Mì Bơ Tỏi", "banh-mi-bo-toi")]
    [InlineData("Phở Bò", "pho-bo")]
    [InlineData("Gỏi Cuốn Tôm Thịt", "goi-cuon-tom-thit")]
    [InlineData("Súp Cua Trứng Bắc Thảo", "sup-cua-trung-bac-thao")]
    [InlineData("Đồ", "do")] // tên chỉ gồm ký tự có dấu, không có ký tự ASCII
    [InlineData("Ăn", "an")]
    [InlineData("Ức", "uc")]
    [InlineData("Ớt", "ot")]
    [InlineData("Ổi", "oi")]
    public void Create_WithVietnameseName_ShouldGenerateAsciiSlug(string name, string expectedSlug)
    {
        // Act
        var category = Category.Create(name);

        // Assert
        category.Slug.Should().Be(expectedSlug);
    }

    [Theory]
    [InlineData("  Món   Chay  ", "Món   Chay", "mon-chay")] // trim 2 đầu; khoảng trắng thừa gộp trong slug
    [InlineData("Bánh--Ngọt", "Bánh--Ngọt", "banh-ngot")] // gộp dấu '-' lặp lại
    [InlineData("!!!Đồ Ăn Vặt!!!", "!!!Đồ Ăn Vặt!!!", "do-an-vat")] // ký tự đặc biệt ở đầu/cuối
    [InlineData("Cà Phê & Bánh Ngọt", "Cà Phê & Bánh Ngọt", "ca-phe-banh-ngot")] // ký tự đặc biệt ở giữa
    [InlineData("Bánh Flan 100% Ngon!", "Bánh Flan 100% Ngon!", "banh-flan-100-ngon")] // giữ chữ số
    [InlineData("Món 5 Tầng", "Món 5 Tầng", "mon-5-tang")]
    public void Create_WithWhitespaceAndSpecialCharacters_ShouldNormalizeSlug(
        string name,
        string expectedName,
        string expectedSlug)
    {
        // Act
        var category = Category.Create(name);

        // Assert
        category.Name.Should().Be(expectedName);
        category.Slug.Should().Be(expectedSlug);
    }

    // Domain là nơi duy nhất kiểm tra khả năng sinh slug;
    // validator của Application không lặp lại rule này (xem CreateCategoryCommandValidatorTests).
    [Theory]
    [InlineData("!!!")]
    [InlineData("---")]
    [InlineData("...")]
    public void Create_WithNameWithoutSlugCharacters_ShouldThrowArgumentException(string name)
    {
        // Act
        var act = () => Category.Create(name);

        // Assert: tên không còn ký tự a-z/0-9 nên không sinh được slug
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameAtMaximumLength_ShouldSucceed()
    {
        // Arrange
        var name = new string('a', Category.MaxNameLength);

        // Act
        var category = Category.Create(name);

        // Assert
        category.Name.Should().HaveLength(Category.MaxNameLength);
        category.Slug.Should().Be(name);
    }

    // Group decision: the slug is generated once in Create and is NOT regenerated on Update,
    // so renaming a category must not break the existing /api/v1/categories/{slug} URL.
    [Fact]
    public void Update_ShouldKeepExistingSlug()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng");

        // Act
        category.Update("  Bánh Ngọt  ", null);

        // Assert
        category.Name.Should().Be("Bánh Ngọt");
        category.Slug.Should().Be("mon-trang-mieng");
    }

    [Fact]
    public void Update_Repeatedly_ShouldKeepSlugGeneratedOnCreate()
    {
        // Arrange
        var category = Category.Create("Món Tráng Miệng");

        // Act
        category.Update("Bánh Ngọt", null);
        category.Update("Bánh Mì", "Mô tả");

        // Assert
        category.Name.Should().Be("Bánh Mì");
        category.Slug.Should().Be("mon-trang-mieng");
    }
}

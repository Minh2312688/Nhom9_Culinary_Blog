using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Commands.CreateCategory;

public class CreateCategoryCommandValidatorTests
{
    private readonly CreateCategoryCommandValidator _validator = new();

    [Theory]
    [InlineData("Bánh Ngọt", null)]
    [InlineData("Bánh Ngọt", "Mô tả món bánh")]
    [InlineData("Ab", "Mô tả")]
    public void Validate_ValidCommand_ShouldNotHaveErrors(string name, string? description)
    {
        // Arrange
        var command = new CreateCategoryCommand(name, description);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingName_ShouldHaveError(string? name)
    {
        // Arrange
        var command = new CreateCategoryCommand(name!, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCategoryCommand.Name));
    }

    [Theory]
    [InlineData("A")]
    [InlineData(" A ")]
    public void Validate_NameShorterThanMinimum_ShouldHaveError(string name)
    {
        // Arrange
        var command = new CreateCategoryCommand(name, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCategoryCommand.Name));
    }

    [Fact]
    public void Validate_NameLongerThanMaximum_ShouldHaveError()
    {
        // Arrange
        var command = new CreateCategoryCommand(new string('a', Category.MaxNameLength + 1), null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCategoryCommand.Name));
    }

    [Fact]
    public void Validate_NameAtMaximumLength_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateCategoryCommand(new string('a', Category.MaxNameLength), null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Đồ")]
    [InlineData("Ăn")]
    [InlineData("Ức")]
    [InlineData("Ớt")]
    [InlineData("Ổi")]
    public void Validate_NameWithOnlyVietnameseDiacritics_ShouldNotHaveErrors(string name)
    {
        // Arrange: tên chỉ gồm ký tự có dấu vẫn sinh được slug ("Đồ" -> "do", "Ăn" -> "an")
        var command = new CreateCategoryCommand(name, null);

        // Act
        var result = _validator.Validate(command);

        // Assert: validator không được từ chối tên tiếng Việt hợp lệ
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NameWithoutAnySlugCharacter_ShouldNotHaveErrors_BecauseDomainOwnsSlugRule()
    {
        // Arrange: validator chỉ kiểm tra bắt buộc + độ dài;
        // Category domain mới là nơi ném ArgumentException khi tên không sinh được slug.
        var command = new CreateCategoryCommand("!!!", null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DescriptionLongerThanMaximum_ShouldHaveError()
    {
        // Arrange
        var command = new CreateCategoryCommand(
            "Bánh Ngọt",
            new string('a', Category.MaxDescriptionLength + 1));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCategoryCommand.Description));
    }

    [Fact]
    public void Validate_DescriptionAtMaximumLength_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new CreateCategoryCommand(
            "Bánh Ngọt",
            new string('a', Category.MaxDescriptionLength));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

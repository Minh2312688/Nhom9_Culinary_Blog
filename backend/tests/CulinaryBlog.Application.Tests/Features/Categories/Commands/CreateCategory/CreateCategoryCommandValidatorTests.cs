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

    // Bug: tên "!!!" qua được validator (chỉ kiểm tra bắt buộc + độ dài) nên handler gọi
    // Category.Create và Domain ném ArgumentException => API trả 500 thay vì 400.
    // Validator giờ chặn sớm, nhưng vẫn không sở hữu thuật toán slug: quy tắc "sinh được slug"
    // do Domain quyết định qua helper Category.CanCreateSlug dùng chung với Create.
    [Theory]
    [InlineData("!!!")]
    [InlineData("---")]
    [InlineData("...")]
    [InlineData("  !!!  ")]
    public void Validate_NameWithoutAnySlugCharacter_ShouldHaveError(string name)
    {
        // Arrange: tên không còn ký tự chữ/số nên Category.Create ném ArgumentException
        var command = new CreateCategoryCommand(name, null);

        // Act
        var result = _validator.Validate(command);

        // Assert: validator từ chối trước khi Domain phải ném exception
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCategoryCommand.Name));
    }

    // Bug: Name chứa HTML markup (<b>, <script>, <img ...>) từng qua được validator nên
    // markup được lưu thẳng vào DB và trả lại cho client. Validator giờ chặn sớm bằng
    // HtmlMarkup.Contains (logic dùng chung với UpdateCategoryCommandValidator) nên API
    // trả 400 Problem Details thay vì nhận dữ liệu có markup.
    [Theory]
    [InlineData("<b>Bánh ngọt</b>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x>")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<b>Bánh Ngọt")]
    [InlineData("Bánh <br/> Ngọt")]
    [InlineData("</b>")]
    [InlineData("<!-- chú thích -->")]
    public void Validate_NameWithHtmlMarkup_ShouldHaveError(string name)
    {
        // Arrange
        var command = new CreateCategoryCommand(name, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateCategoryCommand.Name));
    }

    // Ký tự so sánh "bé hơn/lớn hơn" trong text thuần không phải markup nên không được chặn.
    [Theory]
    [InlineData("Món < 30 phút")]
    [InlineData("Bún <3")]
    [InlineData("Giá 4>2")]
    [InlineData("Đồ ăn (ngon) 100%")]
    [InlineData("Bánh <b")]
    public void Validate_NameWithComparisonSymbolButNoMarkup_ShouldNotHaveErrors(string name)
    {
        // Arrange
        var command = new CreateCategoryCommand(name, null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
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

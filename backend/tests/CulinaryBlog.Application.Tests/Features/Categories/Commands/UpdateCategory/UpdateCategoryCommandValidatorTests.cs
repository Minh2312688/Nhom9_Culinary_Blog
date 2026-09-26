using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Commands.UpdateCategory;

public class UpdateCategoryCommandValidatorTests
{
    private static UpdateCategoryCommand ValidCommand(
        string name = "Bánh Ngọt",
        string? description = null,
        string? imageUrl = null,
        int orderIndex = 0) =>
        new(Guid.NewGuid(), name, description, imageUrl, orderIndex);

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveErrors()
    {
        // Act
        var result = new UpdateCategoryCommandValidator().Validate(
            ValidCommand("Bánh Ngọt", "Mô tả", "https://cdn.test/a.jpg", 3));

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_ShouldHaveError()
    {
        // Act
        var result = new UpdateCategoryCommandValidator().Validate(
            new UpdateCategoryCommand(Guid.Empty, "Bánh Ngọt", null, null, 0));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateCategoryCommand.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A")]
    public void Validate_InvalidName_ShouldHaveError(string? name)
    {
        // Act
        var result = new UpdateCategoryCommandValidator().Validate(ValidCommand(name!));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateCategoryCommand.Name));
    }

    [Fact]
    public void Validate_NameLongerThanMaximum_ShouldHaveError()
    {
        // Act
        var result = new UpdateCategoryCommandValidator()
            .Validate(ValidCommand(new string('a', Category.MaxNameLength + 1)));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateCategoryCommand.Name));
    }

    [Fact]
    public void Validate_DescriptionLongerThanMaximum_ShouldHaveError()
    {
        // Act
        var result = new UpdateCategoryCommandValidator()
            .Validate(ValidCommand(description: new string('a', Category.MaxDescriptionLength + 1)));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(UpdateCategoryCommand.Description));
    }

    [Fact]
    public void Validate_ImageUrlLongerThanMaximum_ShouldHaveError()
    {
        // Act
        var result = new UpdateCategoryCommandValidator()
            .Validate(ValidCommand(imageUrl: new string('a', Category.MaxImageUrlLength + 1)));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpdateCategoryCommand.ImageUrl));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_NegativeOrderIndex_ShouldHaveError(int orderIndex)
    {
        // Act
        var result = new UpdateCategoryCommandValidator().Validate(ValidCommand(orderIndex: orderIndex));

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(UpdateCategoryCommand.OrderIndex));
    }

    [Fact]
    public void Validate_ZeroOrderIndex_ShouldNotHaveErrors()
    {
        // Act
        var result = new UpdateCategoryCommandValidator().Validate(ValidCommand(orderIndex: 0));

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
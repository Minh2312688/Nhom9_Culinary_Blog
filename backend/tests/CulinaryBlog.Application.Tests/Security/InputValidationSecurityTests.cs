using CulinaryBlog.Application.Features.Auth.Profile;
using CulinaryBlog.Application.Features.Auth.Register;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Recipes.Commands;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Security;

/// <summary>
/// NFR-SEC-004: Unit tests verifying that plain-text fields reject active HTML markup
/// (&lt;script&gt;, &lt;img ... onerror&gt;, &lt;svg ... onload&gt;) across Auth, Category, and Recipe commands,
/// while safely accepting Vietnamese diacritics and harmless comparison symbols (e.g. "Món &lt; 30 phút", "Bún &lt;3").
/// </summary>
public class InputValidationSecurityTests
{
    private readonly RegisterCommandValidator _registerValidator = new();
    private readonly UpdateProfileCommandValidator _updateProfileValidator = new();
    private readonly CreateCategoryCommandValidator _createCategoryValidator = new();
    private readonly UpdateCategoryCommandValidator _updateCategoryValidator = new();
    private readonly CreateRecipeCommandValidator _createRecipeValidator = new();
    private readonly UpdateRecipeCommandValidator _updateRecipeValidator = new();
    private readonly AddRecipeStepCommandValidator _addStepValidator = new();
    private readonly UpdateRecipeStepCommandValidator _updateStepValidator = new();

    #region Auth Validation Tests

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void Register_DisplayNameWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = new RegisterCommand("chef@example.com", "P@ssword123", markup);
        var result = _registerValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterCommand.DisplayName) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("Nguyễn Văn A")]
    [InlineData("Đầu bếp <3")]
    [InlineData("Chef 2026")]
    public void Register_ValidDisplayName_ShouldPass(string validName)
    {
        var command = new RegisterCommand("chef@example.com", "P@ssword123", validName);
        var result = _registerValidator.Validate(command);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(RegisterCommand.DisplayName));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void UpdateProfile_DisplayNameWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = new UpdateProfileCommand("user123", markup, null, "Normal bio");
        var result = _updateProfileValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProfileCommand.DisplayName) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void UpdateProfile_BioWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = new UpdateProfileCommand("user123", "Nguyễn Văn A", null, markup);
        var result = _updateProfileValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateProfileCommand.Bio) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("Đầu bếp Nam Định", "Yêu thích ẩm thực Việt Nam < 30 phút, nấu nướng với đam mê <3")]
    [InlineData("Phạm Hoàng Long", "Chia sẻ công thức món ăn truyền thống.")]
    public void UpdateProfile_ValidInputs_ShouldPass(string displayName, string bio)
    {
        var command = new UpdateProfileCommand("user123", displayName, null, bio);
        var result = _updateProfileValidator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Category Validation Tests

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void CreateCategory_DescriptionWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = new CreateCategoryCommand("Món Tráng Miệng", markup);
        var result = _createCategoryValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCategoryCommand.Description) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("Món khai vị nhanh gọn < 30 phút")]
    [InlineData("Bún <3 và các món sợi truyền thống")]
    [InlineData("Đặc sản miền Bắc Việt Nam")]
    public void CreateCategory_ValidDescription_ShouldPass(string description)
    {
        var command = new CreateCategoryCommand("Món Khai Vị", description);
        var result = _createCategoryValidator.Validate(command);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(CreateCategoryCommand.Description));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void UpdateCategory_DescriptionWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = new UpdateCategoryCommand(Guid.NewGuid(), "Món Tráng Miệng", markup, null, 1);
        var result = _updateCategoryValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCategoryCommand.Description) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("Món < 30 phút")]
    [InlineData("Bún <3")]
    [InlineData("Công thức đồ uống thanh mát")]
    public void UpdateCategory_ValidDescription_ShouldPass(string description)
    {
        var command = new UpdateCategoryCommand(Guid.NewGuid(), "Đồ Uống", description, null, 0);
        var result = _updateCategoryValidator.Validate(command);

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(UpdateCategoryCommand.Description));
    }

    #endregion

    #region Recipe Validation Tests

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void CreateRecipe_TitleWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = BuildValidCreateRecipeCommand() with { Title = markup };
        var result = _createRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRecipeCommand.Title) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    public void CreateRecipe_DescriptionWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = BuildValidCreateRecipeCommand() with { Description = markup };
        var result = _createRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateRecipeCommand.Description) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void CreateRecipe_IngredientNameWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var ingredients = new List<RecipeIngredientInput>
        {
            new(markup, 200, "g", "Ghi chú")
        };
        var command = BuildValidCreateRecipeCommand() with { Ingredients = ingredients };
        var result = _createRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Ingredients") &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void CreateRecipe_IngredientNotesWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var ingredients = new List<RecipeIngredientInput>
        {
            new("Thịt bò", 200, "g", markup)
        };
        var command = BuildValidCreateRecipeCommand() with { Ingredients = ingredients };
        var result = _createRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Ingredients") &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void CreateRecipe_StepTitleWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var steps = new List<RecipeStepInput>
        {
            new(markup, "Mô tả bước làm", 10, null)
        };
        var command = BuildValidCreateRecipeCommand() with { Steps = steps };
        var result = _createRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Steps") &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void CreateRecipe_StepDescriptionWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var steps = new List<RecipeStepInput>
        {
            new("Bước 1", markup, 10, null)
        };
        var command = BuildValidCreateRecipeCommand() with { Steps = steps };
        var result = _createRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.StartsWith("Steps") &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Fact]
    public void CreateRecipe_ValidVietnameseTextAndComparisonSymbols_ShouldPass()
    {
        var command = new CreateRecipeCommand(
            Title: "Phở Bò Nam Định Truyền Thống",
            Description: "Món ăn thanh mát nấu dưới < 30 phút, phù hợp gia đình <3",
            CategoryId: Guid.NewGuid(),
            PrepTimeMinutes: 15,
            CookTimeMinutes: 20,
            Servings: 4,
            Difficulty: "Medium",
            Ingredients: new List<RecipeIngredientInput>
            {
                new("Thịt bò tái < 500g", 500, "g", "Thái mỏng < 2mm"),
                new("Bánh phở tươi", 400, "g", "Trụng qua nước sôi <3")
            },
            Steps: new List<RecipeStepInput>
            {
                new("Sơ chế nguyên liệu", "Rửa sạch thịt bò trong nước muối ấm < 40 độ C", 10, null),
                new("Nấu nước dùng", "Nấu sôi nước hầm xương < 20 phút rồi nêm nếm", 20, null)
            });

        var result = _createRecipeValidator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void UpdateRecipe_TitleWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = BuildValidUpdateRecipeCommand() with { Title = markup };
        var result = _updateRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRecipeCommand.Title) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void UpdateRecipe_DescriptionWithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var command = BuildValidUpdateRecipeCommand() with { Description = markup };
        var result = _updateRecipeValidator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRecipeCommand.Description) &&
                                           e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void AddRecipeStep_WithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var titleCommand = new AddRecipeStepCommand(Guid.NewGuid(), markup, "Mô tả bước", 10, null);
        var titleResult = _addStepValidator.Validate(titleCommand);
        titleResult.IsValid.Should().BeFalse();
        titleResult.Errors.Should().Contain(e => e.PropertyName == nameof(AddRecipeStepCommand.Title) &&
                                                e.ErrorMessage.Contains("must not contain HTML markup"));

        var descCommand = new AddRecipeStepCommand(Guid.NewGuid(), "Bước 1", markup, 10, null);
        var descResult = _addStepValidator.Validate(descCommand);
        descResult.IsValid.Should().BeFalse();
        descResult.Errors.Should().Contain(e => e.PropertyName == nameof(AddRecipeStepCommand.Description) &&
                                                e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public void UpdateRecipeStep_WithHtmlMarkup_ShouldBeRejected(string markup)
    {
        var titleCommand = new UpdateRecipeStepCommand(Guid.NewGuid(), Guid.NewGuid(), markup, "Mô tả", 10, null);
        var titleResult = _updateStepValidator.Validate(titleCommand);
        titleResult.IsValid.Should().BeFalse();
        titleResult.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRecipeStepCommand.Title) &&
                                                e.ErrorMessage.Contains("must not contain HTML markup"));

        var descCommand = new UpdateRecipeStepCommand(Guid.NewGuid(), Guid.NewGuid(), "Bước 1", markup, 10, null);
        var descResult = _updateStepValidator.Validate(descCommand);
        descResult.IsValid.Should().BeFalse();
        descResult.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateRecipeStepCommand.Description) &&
                                                e.ErrorMessage.Contains("must not contain HTML markup"));
    }

    [Fact]
    public void StepCommands_ValidVietnameseAndComparisons_ShouldPass()
    {
        var addCommand = new AddRecipeStepCommand(Guid.NewGuid(), "Bước chuẩn bị <3", "Nấu trong thời gian < 15 phút", 15, null);
        var addResult = _addStepValidator.Validate(addCommand);
        addResult.IsValid.Should().BeTrue();

        var updateCommand = new UpdateRecipeStepCommand(Guid.NewGuid(), Guid.NewGuid(), "Bước hoàn thiện < 5 phút", "Trình bày ra đĩa đẹp mắt", 5, null);
        var updateResult = _updateStepValidator.Validate(updateCommand);
        updateResult.IsValid.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static CreateRecipeCommand BuildValidCreateRecipeCommand() => new(
        Title: "Công Thức Nấu Phở Bò",
        Description: "Mô tả công thức nấu phở truyền thống",
        CategoryId: Guid.NewGuid(),
        PrepTimeMinutes: 10,
        CookTimeMinutes: 20,
        Servings: 2,
        Difficulty: "Easy",
        Ingredients: new List<RecipeIngredientInput>
        {
            new("Bánh phở", 200, "g", "Tươi ngon")
        },
        Steps: new List<RecipeStepInput>
        {
            new("Bước 1", "Trụng phở qua nước sôi", 5, null)
        });

    private static UpdateRecipeCommand BuildValidUpdateRecipeCommand() => new(
        Id: Guid.NewGuid(),
        Title: "Công Thức Cập Nhật Mới",
        Description: "Mô tả công thức cập nhật",
        CategoryId: Guid.NewGuid(),
        PrepTimeMinutes: 10,
        CookTimeMinutes: 20,
        Servings: 2,
        Difficulty: "Easy",
        RowVersion: new byte[] { 1, 2, 3, 4 },
        Ingredients: new List<RecipeIngredientInput>
        {
            new("Bánh phở", 200, "g", "Tươi ngon")
        },
        Steps: new List<RecipeStepInput>
        {
            new("Bước 1", "Trụng phở qua nước sôi", 5, null)
        });

    #endregion
}

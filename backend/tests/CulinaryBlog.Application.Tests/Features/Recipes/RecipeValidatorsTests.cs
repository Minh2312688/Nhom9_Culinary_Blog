using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Application.Features.Recipes.Queries;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class RecipeValidatorsTests
{
    [Fact]
    public async Task CreateRecipe_ShouldRejectInvalidCoreFields()
    {
        var validator = new CreateRecipeCommandValidator();
        var result = await validator.ValidateAsync(new CreateRecipeCommand(
            "abc", null, Guid.Empty, -1, -1, 0, ""));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateRecipeCommand.Title));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateRecipeCommand.Servings));
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("medium")]
    [InlineData("HARD")]
    [InlineData("Expert")]
    public async Task CreateRecipe_ShouldAcceptDifficultyEnumNamesIgnoringCase(string difficulty)
    {
        var validator = new CreateRecipeCommandValidator();

        var result = await validator.ValidateAsync(new CreateRecipeCommand(
            "Valid recipe", null, Guid.NewGuid(), 0, 0, 1, difficulty));

        Assert.DoesNotContain(result.Errors, error => error.PropertyName == nameof(CreateRecipeCommand.Difficulty));
    }

    [Theory]
    [InlineData("Novice")]
    [InlineData("1")]
    public async Task CreateRecipe_ShouldRejectDifficultyOutsideEnum(string difficulty)
    {
        var validator = new CreateRecipeCommandValidator();

        var result = await validator.ValidateAsync(new CreateRecipeCommand(
            "Valid recipe", null, Guid.NewGuid(), 0, 0, 1, difficulty));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateRecipeCommand.Difficulty));
    }

    [Fact]
    public async Task CreateRecipe_ShouldAllowMissingIngredientQuantityButRejectNonPositiveQuantity()
    {
        var validator = new CreateRecipeCommandValidator();
        var validWithoutQuantity = new CreateRecipeCommand(
            "Valid recipe", null, Guid.NewGuid(), 5, 10, 2, "Easy",
            [new RecipeIngredientInput("Salt", null, null, null)]);
        var invalidQuantity = validWithoutQuantity with
        {
            Ingredients = [new RecipeIngredientInput("Salt", 0, "g", null)]
        };

        Assert.True((await validator.ValidateAsync(validWithoutQuantity)).IsValid);
        var result = await validator.ValidateAsync(invalidQuantity);
        Assert.Contains(result.Errors, error => error.PropertyName == "Ingredients[0].Quantity");
    }

    [Fact]
    public async Task UpdateRecipe_ShouldRejectNegativeIngredientQuantity()
    {
        var validator = new UpdateRecipeCommandValidator();
        var command = new UpdateRecipeCommand(
            Guid.NewGuid(), "Valid recipe", null, Guid.NewGuid(), 5, 10, 2, "Easy", [1],
            [new RecipeIngredientInput("Salt", -1, "g", null)]);

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Ingredients[0].Quantity");
    }

    [Fact]
    public async Task UpdateRecipe_ShouldRequireRowVersion()
    {
        var validator = new UpdateRecipeCommandValidator();
        var result = await validator.ValidateAsync(new UpdateRecipeCommand(
            Guid.NewGuid(), "Valid recipe", null, Guid.NewGuid(), 5, 10, 2, "Easy", []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateRecipeCommand.RowVersion));
    }

    [Fact]
    public async Task UpdateRecipe_ShouldRejectDifficultyOutsideEnum()
    {
        var validator = new UpdateRecipeCommandValidator();
        var command = new UpdateRecipeCommand(
            Guid.NewGuid(), "Valid recipe", null, Guid.NewGuid(), 5, 10, 2, "Novice", [1]);

        var result = await validator.ValidateAsync(command);

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateRecipeCommand.Difficulty));
    }

    [Fact]
    public async Task GetRecipes_ShouldRejectUnsupportedSorting()
    {
        var validator = new GetRecipesQueryValidator();
        var result = await validator.ValidateAsync(new GetRecipesQuery(SortBy: "unknown", SortOrder: "sideways"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.SortBy));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.SortOrder));
    }

    [Fact]
    public async Task GetRecipes_SearchLength100IsAcceptedAnd101IsRejected()
    {
        var validator = new GetRecipesQueryValidator();

        var accepted = await validator.ValidateAsync(new GetRecipesQuery(Search: new string('a', 100)));
        var rejected = await validator.ValidateAsync(new GetRecipesQuery(Search: new string('a', 101)));

        Assert.DoesNotContain(accepted.Errors, error => error.PropertyName == nameof(GetRecipesQuery.Search));
        Assert.Contains(rejected.Errors, error => error.PropertyName == nameof(GetRecipesQuery.Search));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRecipes_ShouldRejectNonPositiveMaxCookTime(int maxCookTime)
    {
        var validator = new GetRecipesQueryValidator();

        var result = await validator.ValidateAsync(new GetRecipesQuery(MaxCookTime: maxCookTime));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.MaxCookTime));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRecipes_ShouldRejectNonPositiveMinServings(int minServings)
    {
        var validator = new GetRecipesQueryValidator();

        var result = await validator.ValidateAsync(new GetRecipesQuery(MinServings: minServings));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.MinServings));
    }

    [Theory]
    [InlineData("Easy")]
    [InlineData("medium")]
    [InlineData("HARD")]
    public async Task GetRecipes_ShouldAcceptSupportedDifficultyIgnoringCase(string difficulty)
    {
        var validator = new GetRecipesQueryValidator();

        var result = await validator.ValidateAsync(new GetRecipesQuery(Difficulty: difficulty));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Expert")]
    [InlineData("Impossible")]
    public async Task GetRecipes_ShouldRejectUnsupportedDifficulty(string difficulty)
    {
        var validator = new GetRecipesQueryValidator();

        var result = await validator.ValidateAsync(new GetRecipesQuery(Difficulty: difficulty));

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.Difficulty));
    }
}

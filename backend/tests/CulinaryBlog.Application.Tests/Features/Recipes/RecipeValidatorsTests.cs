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
    public async Task GetRecipes_ShouldRejectUnsupportedSorting()
    {
        var validator = new GetRecipesQueryValidator();
        var result = await validator.ValidateAsync(new GetRecipesQuery(SortBy: "unknown", SortOrder: "sideways"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.SortBy));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(GetRecipesQuery.SortOrder));
    }
}

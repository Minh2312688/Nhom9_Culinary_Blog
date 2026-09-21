using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Features.Recipes;

internal static class RecipeMapping
{
    public static RecipeSummaryDto ToSummary(this Recipe recipe) => new(
        recipe.Id, recipe.Title, recipe.Slug, recipe.Description, recipe.Difficulty,
        recipe.CookTimeMinutes, recipe.Servings, recipe.Status, recipe.CategoryId,
        recipe.AuthorId, recipe.RowVersion);

    public static RecipeDetailDto ToDetail(this Recipe recipe) => new(
        recipe.Id, recipe.Title, recipe.Slug, recipe.Description, recipe.PrepTimeMinutes,
        recipe.CookTimeMinutes, recipe.Servings, recipe.Difficulty, recipe.Status,
        recipe.CategoryId, recipe.AuthorId, recipe.RowVersion,
        recipe.Ingredients.OrderBy(x => x.OrderIndex).Select(x => new RecipeIngredientDto(
            x.Id, x.Name, x.Quantity, x.Unit, x.Notes, x.OrderIndex)).ToList(),
        recipe.Steps.OrderBy(x => x.StepNumber).Select(x => new RecipeStepDto(
            x.Id, x.StepNumber, x.Title, x.Description, x.DurationMinutes, x.ImageUrl)).ToList(),
        recipe.Images.OrderBy(x => x.OrderIndex).Select(x => new RecipeImageDto(
            x.Id, x.OriginalUrl, x.MediumUrl, x.ThumbnailUrl, x.AltText, x.IsPrimary, x.OrderIndex)).ToList(),
        recipe.Nutrition is null ? null : new RecipeNutritionDto(
            recipe.Nutrition.Calories, recipe.Nutrition.Protein, recipe.Nutrition.Carbohydrates,
            recipe.Nutrition.Fat, recipe.Nutrition.Fiber, recipe.Nutrition.Sodium));
}

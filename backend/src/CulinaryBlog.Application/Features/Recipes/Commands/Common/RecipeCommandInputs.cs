namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record RecipeIngredientInput(string Name, decimal? Quantity, string? Unit, string? Notes, int OrderIndex = 0);
public sealed record RecipeStepInput(string? Title, string Description, int? DurationMinutes, string? ImageUrl);
public sealed record RecipeNutritionInput(int Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal Fiber, decimal Sodium);

internal static class RecipeCommandSlug
{
    public static string Slugify(string value) => new string(value.Trim().ToLowerInvariant()
        .Normalize(global::System.Text.NormalizationForm.FormD)
        .Where(x => global::System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x) != global::System.Globalization.UnicodeCategory.NonSpacingMark)
        .ToArray()).Replace('đ', 'd').Replace(' ', '-');
}

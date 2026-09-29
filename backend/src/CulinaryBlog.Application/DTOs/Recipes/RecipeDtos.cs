using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.DTOs.Recipes;

public sealed record RecipeSummaryDto(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string Difficulty,
    int CookTimeMinutes,
    int Servings,
    RecipeStatus Status,
    Guid CategoryId,
    string AuthorId,
    byte[] RowVersion);

public sealed record RecipeDetailDto(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int Servings,
    string Difficulty,
    RecipeStatus Status,
    Guid CategoryId,
    string AuthorId,
    byte[] RowVersion,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<RecipeStepDto> Steps,
    IReadOnlyList<RecipeImageDto> Images,
    RecipeNutritionDto? Nutrition);

public sealed record RecipeIngredientDto(Guid Id, string Name, decimal? Quantity, string? Unit, string? Notes, int OrderIndex);
public sealed record RecipeStepDto(Guid Id, int StepNumber, string? Title, string Description, int? DurationMinutes, string? ImageUrl);
public sealed record RecipeImageDto(Guid Id, string OriginalUrl, string? MediumUrl, string? ThumbnailUrl, string? AltText, bool IsPrimary, int OrderIndex);
public sealed record RecipeNutritionDto(int Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal Fiber, decimal Sodium);

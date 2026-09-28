namespace CulinaryBlog.API.Contracts.Recipes;

public sealed record AddRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex = 0);

public sealed record ReplaceRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int OrderIndex);

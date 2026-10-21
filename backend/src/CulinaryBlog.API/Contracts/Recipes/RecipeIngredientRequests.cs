namespace CulinaryBlog.API.Contracts.Recipes;

public sealed record AddRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex);

/// <summary>PUT replaces all editable ingredient fields; omitted nullable values clear existing values.</summary>
public sealed record ReplaceRecipeIngredientRequest(
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex);

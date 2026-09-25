namespace CulinaryBlog.API.Contracts.Recipes;

/// <summary>StepNumber is intentionally omitted; the server assigns it.</summary>
public sealed record AddRecipeStepRequest(
    string Title,
    string Description,
    int? DurationMinutes,
    string? ImageUrl);

/// <summary>PUT replaces all editable step fields. StepNumber remains server-managed.</summary>
public sealed record ReplaceRecipeStepRequest(
    string Title,
    string Description,
    int? DurationMinutes,
    string? ImageUrl);

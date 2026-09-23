namespace CulinaryBlog.Application.Abstractions.Search;

/// <summary>
/// Read-only search result model for Recipe search (Lab 03 - Personal Scope: Nguyen Pham Phu Nam).
/// </summary>
public record RecipeSearchResult(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    Guid CategoryId,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int Servings,
    int Difficulty
);

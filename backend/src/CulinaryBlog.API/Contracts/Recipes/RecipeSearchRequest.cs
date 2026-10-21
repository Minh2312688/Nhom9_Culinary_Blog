namespace CulinaryBlog.API.Contracts.Recipes;

public sealed record RecipeSearchRequest(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? SortBy = null,
    string? SortOrder = null);

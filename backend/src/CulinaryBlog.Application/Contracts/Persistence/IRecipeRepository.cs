using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;

namespace CulinaryBlog.Application.Contracts.Persistence;

public interface IRecipeRepository
{
    IQueryable<Recipe> Query { get; }
    IQueryable<RecipeStep> Steps { get; }
    Task<PaginatedResult<RecipeSummaryDto>> SearchAsync(
        string query, string? userId, bool isAdmin, int page, int pageSize,
        CancellationToken cancellationToken = default);
    void Add(Recipe recipe);
    void Remove(Recipe recipe);
    void AddStep(RecipeStep step);
}

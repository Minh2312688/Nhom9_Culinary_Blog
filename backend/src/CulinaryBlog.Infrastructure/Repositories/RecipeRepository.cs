using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using NpgsqlTypes;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class RecipeRepository(IApplicationDbContext context) : IRecipeRepository
{
    public IQueryable<Recipe> Query => context.Recipes;
    public IQueryable<RecipeStep> Steps => context.RecipeSteps;

    public async Task<PaginatedResult<RecipeSummaryDto>> SearchAsync(
        string query, string? userId, bool isAdmin, int page, int pageSize,
        Guid? categoryId = null, RecipeDifficulty? difficulty = null, int? maxCookTime = null, int? minServings = null,
        CancellationToken cancellationToken = default, string? sortBy = null, string? sortOrder = null)
    {
        var terms = Regex.Matches(query, @"[\p{L}\p{N}]+")
            .Select(match => $"{match.Value}:*")
            .ToArray();
        if (terms.Length == 0)
            return new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), 0, page, pageSize);

        var tsQueryText = string.Join(" & ", terms);
        var recipes = context.Recipes.AsNoTracking();
        recipes = isAdmin
            ? recipes
            : recipes.Where(x => x.Status == RecipeStatus.Published || (userId != null && x.AuthorId == userId));

        if (categoryId.HasValue) recipes = recipes.Where(x => x.CategoryId == categoryId.Value);
        if (difficulty.HasValue) recipes = recipes.Where(x => x.Difficulty == difficulty.Value);
        if (maxCookTime.HasValue) recipes = recipes.Where(x => x.CookTimeMinutes <= maxCookTime.Value);
        if (minServings.HasValue) recipes = recipes.Where(x => x.Servings >= minServings.Value);

        recipes = recipes.Where(x => EF.Property<NpgsqlTsVector>(x, "SearchVector")
            .Matches(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQueryText))));
        var totalCount = await recipes.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (page > totalPages)
            return new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), totalCount, page, pageSize);

        var descending = !(sortOrder?.Equals("asc", StringComparison.OrdinalIgnoreCase) ?? false);
        var ordered = string.IsNullOrWhiteSpace(sortBy)
            ? recipes
                .OrderByDescending(x => EF.Property<NpgsqlTsVector>(x, "SearchVector")
                    .RankCoverDensity(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQueryText))))
                .ThenByDescending(x => x.CreatedAt)
                .ThenBy(x => x.Id)
            : sortBy.ToLowerInvariant() switch
            {
                "title" => descending ? recipes.OrderByDescending(x => x.Title).ThenBy(x => x.Id) : recipes.OrderBy(x => x.Title).ThenBy(x => x.Id),
                "cooktime" => descending ? recipes.OrderByDescending(x => x.CookTimeMinutes).ThenBy(x => x.Id) : recipes.OrderBy(x => x.CookTimeMinutes).ThenBy(x => x.Id),
                "preptime" => descending ? recipes.OrderByDescending(x => x.PrepTimeMinutes).ThenBy(x => x.Id) : recipes.OrderBy(x => x.PrepTimeMinutes).ThenBy(x => x.Id),
                "createdat" => descending ? recipes.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id) : recipes.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
                _ => throw new ArgumentException("Unsupported recipe sort field.", nameof(sortBy))
            };
        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Title, x.Slug, x.Description, x.Difficulty, x.CookTimeMinutes,
                x.Servings, x.Status, x.CategoryId, x.AuthorId, x.RowVersion
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(x => new RecipeSummaryDto(x.Id, x.Title, x.Slug, x.Description,
            x.Difficulty.ToString(), x.CookTimeMinutes, x.Servings, x.Status, x.CategoryId,
            x.AuthorId, x.RowVersion)).ToList();

        return new PaginatedResult<RecipeSummaryDto>(items, totalCount, page, pageSize);
    }

    public void Add(Recipe recipe) => context.Recipes.Add(recipe);
    public void Remove(Recipe recipe) => context.Recipes.Remove(recipe);
    public void AddStep(RecipeStep step) => context.RecipeSteps.Add(step);
}

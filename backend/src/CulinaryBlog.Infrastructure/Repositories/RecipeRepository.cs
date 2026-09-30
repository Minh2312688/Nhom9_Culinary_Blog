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
        CancellationToken cancellationToken = default)
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

        recipes = recipes.Where(x => EF.Property<NpgsqlTsVector>(x, "SearchVector")
            .Matches(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQueryText))));
        var totalCount = await recipes.CountAsync(cancellationToken);
        var items = await recipes
            .OrderByDescending(x => EF.Property<NpgsqlTsVector>(x, "SearchVector")
                .RankCoverDensity(EF.Functions.ToTsQuery("simple", EF.Functions.Unaccent(tsQueryText))))
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RecipeSummaryDto(x.Id, x.Title, x.Slug, x.Description, x.Difficulty,
                x.CookTimeMinutes, x.Servings, x.Status, x.CategoryId, x.AuthorId, x.RowVersion))
            .ToListAsync(cancellationToken);

        return new PaginatedResult<RecipeSummaryDto>(items, totalCount, page, pageSize);
    }

    public void Add(Recipe recipe) => context.Recipes.Add(recipe);
    public void Remove(Recipe recipe) => context.Recipes.Remove(recipe);
    public void AddStep(RecipeStep step) => context.RecipeSteps.Add(step);
}

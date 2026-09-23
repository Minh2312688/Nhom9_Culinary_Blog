using CulinaryBlog.Application.Abstractions.Search;
using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL Full-Text Search implementation for Recipe Search.
/// Merge-safe specialized repository for Lab 03 (Personal Scope: Nguyen Pham Phu Nam).
/// Executes server-side FTS in PostgreSQL using GIN index.
/// </summary>
public class PostgresRecipeSearchRepository : IRecipeSearchRepository
{
    private readonly AuthDbContext _context;

    public PostgresRecipeSearchRepository(AuthDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RecipeSearchResult>> SearchAsync(
        string searchTerm,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Array.Empty<RecipeSearchResult>();
        }

        // Normalize pagination parameters
        if (skip < 0) skip = 0;
        if (take <= 0) take = 20;
        if (take > 100) take = 100;

        var normalizedTerm = searchTerm.Trim();

        var query = _context.Recipes
            .AsNoTracking()
            .Where(r => EF.Functions.ToTsVector("simple", (r.Title ?? "") + " " + (r.Description ?? ""))
                .Matches(EF.Functions.PlainToTsQuery("simple", normalizedTerm)))
            .OrderBy(r => r.Title)
            .Skip(skip)
            .Take(take)
            .Select(r => new RecipeSearchResult(
                r.Id,
                r.Title,
                r.Slug,
                r.Description,
                r.CategoryId,
                r.PrepTimeMinutes,
                r.CookTimeMinutes,
                r.Servings,
                r.Difficulty
            ));

        var results = await query.ToListAsync(cancellationToken);
        return results;
    }
}

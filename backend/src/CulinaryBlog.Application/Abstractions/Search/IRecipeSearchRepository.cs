namespace CulinaryBlog.Application.Abstractions.Search;

/// <summary>
/// Read-only search repository contract for Recipe Full-Text Search.
/// Merge-safe specialized interface for Lab 03 (Personal Scope: Nguyen Pham Phu Nam).
/// Coexists independently with TV2 Recipe CRUD repository.
/// </summary>
public interface IRecipeSearchRepository
{
    Task<IReadOnlyList<RecipeSearchResult>> SearchAsync(
        string searchTerm,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default);
}

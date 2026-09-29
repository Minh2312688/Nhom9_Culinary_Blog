using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Application.Contracts.Persistence;

public interface IRecipeRepository
{
    IQueryable<Recipe> Query { get; }
    IQueryable<RecipeStep> Steps { get; }
    void Add(Recipe recipe);
    void Remove(Recipe recipe);
    void AddStep(RecipeStep step);
}

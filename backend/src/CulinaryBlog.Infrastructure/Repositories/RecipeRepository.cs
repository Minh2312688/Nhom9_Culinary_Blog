using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Infrastructure.Repositories;

public sealed class RecipeRepository(IApplicationDbContext context) : IRecipeRepository
{
    public IQueryable<Recipe> Query => context.Recipes;
    public IQueryable<RecipeStep> Steps => context.RecipeSteps;
    public void Add(Recipe recipe) => context.Recipes.Add(recipe);
    public void Remove(Recipe recipe) => context.Recipes.Remove(recipe);
    public void AddStep(RecipeStep step) => context.RecipeSteps.Add(step);
}

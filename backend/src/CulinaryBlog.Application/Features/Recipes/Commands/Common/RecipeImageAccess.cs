using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

internal static class RecipeImageAccess
{
    public static async Task<Recipe> GetOwnedRecipeAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var recipe = await context.Recipes.SingleOrDefaultAsync(
            item => item.Id == recipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), recipeId);

        if (!currentUser.IsAdmin &&
            (!currentUser.IsAuthenticated || recipe.AuthorId != currentUser.UserId))
        {
            throw new ForbiddenAccessException();
        }

        return recipe;
    }

    public static async Task InvalidateRecipeCacheAsync(
        IRecipeCache cache,
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug.ToLowerInvariant()}", cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
    }
}

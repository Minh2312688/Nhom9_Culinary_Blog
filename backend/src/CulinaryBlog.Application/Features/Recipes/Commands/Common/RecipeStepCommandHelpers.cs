using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

internal static class RecipeStepCommandHelpers
{
    public static async Task<Recipe> GetEditableRecipeAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || (currentUser.UserId is null && !currentUser.IsAdmin))
            throw new ForbiddenAccessException("Authentication is required.");

        var recipe = await context.Recipes.SingleOrDefaultAsync(x => x.Id == recipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), recipeId);
        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId)
            throw new ForbiddenAccessException();
        return recipe;
    }

    public static async Task InvalidateAsync(IRecipeCache cache, string slug, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"recipes:slug:{slug.ToLowerInvariant()}", cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
    }

    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static RecipeStepDto ToStepDto(this RecipeStep step) => new(
        step.Id, step.StepNumber, step.Title, step.Description, step.DurationMinutes, step.ImageUrl);
}

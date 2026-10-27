using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record UpdateRecipeCommand(
    Guid Id,
    string Title,
    string? Description,
    Guid CategoryId,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int Servings,
    string Difficulty,
    byte[] RowVersion,
    IReadOnlyList<RecipeIngredientInput>? Ingredients = null,
    IReadOnlyList<RecipeStepInput>? Steps = null,
    RecipeNutritionInput? Nutrition = null) : IRequest<RecipeDetailDto>;

public sealed class UpdateRecipeCommandHandler : IRequestHandler<UpdateRecipeCommand, RecipeDetailDto>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;

    public UpdateRecipeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IRecipeCache cache)
    { this.context = context; this.currentUser = currentUser; this.cache = cache; }

    public async Task<RecipeDetailDto> Handle(UpdateRecipeCommand request, CancellationToken cancellationToken)
    {
        var recipe = await context.Recipes.Include(x => x.Ingredients).Include(x => x.Steps).Include(x => x.Nutrition)
            .SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken) ?? throw new NotFoundException(nameof(Recipe), request.Id);
        EnsureOwner(recipe);
        if (!await context.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken)) throw new NotFoundException(nameof(Category), request.CategoryId);
        var previousSlug = recipe.Slug;
        context.SetOriginalRowVersion(recipe, request.RowVersion);
        recipe.Title = request.Title.Trim(); recipe.Description = request.Description?.Trim(); recipe.CategoryId = request.CategoryId;
        recipe.PrepTimeMinutes = request.PrepTimeMinutes; recipe.CookTimeMinutes = request.CookTimeMinutes;
        recipe.Servings = request.Servings; recipe.Difficulty = Enum.Parse<RecipeDifficulty>(request.Difficulty, true);
        recipe.Slug = RecipeCommandSlug.Slugify(recipe.Title);
        if (request.Ingredients is not null)
        {
            foreach (var ingredient in recipe.Ingredients) ingredient.IsDeleted = true;
            recipe.Ingredients.Clear();
            foreach (var item in request.Ingredients) recipe.Ingredients.Add(new RecipeIngredient
            {
                Name = item.Name.Trim(), Quantity = item.Quantity, Unit = item.Unit?.Trim(), Notes = item.Notes?.Trim(), OrderIndex = item.OrderIndex
            });
        }
        if (request.Steps is not null)
        {
            foreach (var step in recipe.Steps) step.IsDeleted = true;
            recipe.Steps.Clear();
            var number = 1;
            foreach (var item in request.Steps) recipe.Steps.Add(new RecipeStep
            {
                StepNumber = number++, Title = item.Title?.Trim(), Description = item.Description.Trim(),
                DurationMinutes = item.DurationMinutes, ImageUrl = item.ImageUrl?.Trim()
            });
        }
        if (request.Nutrition is not null)
        {
            recipe.Nutrition = new RecipeNutrition
            {
                Calories = request.Nutrition.Calories, Protein = request.Nutrition.Protein, Carbohydrates = request.Nutrition.Carbohydrates,
                Fat = request.Nutrition.Fat, Fiber = request.Nutrition.Fiber, Sodium = request.Nutrition.Sodium
            };
        }
        try { await context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyException(); }
        await cache.RemoveAsync($"recipes:slug:{previousSlug}", cancellationToken);
        if (!string.Equals(previousSlug, recipe.Slug, StringComparison.OrdinalIgnoreCase))
            await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
        return recipe.ToDetail();
    }

    private void EnsureOwner(Recipe recipe)
    { if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId) throw new ForbiddenAccessException(); }
}

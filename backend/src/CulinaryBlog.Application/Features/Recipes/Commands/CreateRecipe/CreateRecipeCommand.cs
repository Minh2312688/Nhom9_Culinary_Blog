using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record CreateRecipeCommand(
    string Title,
    string? Description,
    Guid CategoryId,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int Servings,
    string Difficulty,
    IReadOnlyList<RecipeIngredientInput>? Ingredients = null,
    IReadOnlyList<RecipeStepInput>? Steps = null,
    RecipeNutritionInput? Nutrition = null) : IRequest<RecipeDetailDto>;

public sealed class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, RecipeDetailDto>
{
    private readonly IRecipeRepository recipes;
    private readonly ICategoryRepository categories;
    private readonly IUnitOfWork unitOfWork;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;

    public CreateRecipeCommandHandler(IRecipeRepository recipes, ICategoryRepository categories,
        IUnitOfWork unitOfWork, ICurrentUserService currentUser, IRecipeCache cache)
    {
        this.recipes = recipes;
        this.categories = categories;
        this.unitOfWork = unitOfWork;
        this.currentUser = currentUser;
        this.cache = cache;
    }

    public async Task<RecipeDetailDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        EnsureCanEdit();
        if (!await categories.Query.AnyAsync(x => x.Id == request.CategoryId, cancellationToken))
            throw new NotFoundException(nameof(Category), request.CategoryId);

        var slug = await CreateUniqueSlugAsync(request.Title, cancellationToken);
        var recipe = new Recipe
        {
            Title = request.Title.Trim(), Slug = slug, Description = request.Description?.Trim(),
            CategoryId = request.CategoryId, AuthorId = currentUser.UserId!,
            PrepTimeMinutes = request.PrepTimeMinutes, CookTimeMinutes = request.CookTimeMinutes,
            Servings = request.Servings, Difficulty = Enum.Parse<RecipeDifficulty>(request.Difficulty, true), Status = RecipeStatus.Draft
        };
        AddChildren(recipe, request.Ingredients, request.Steps, request.Nutrition);
        recipes.Add(recipe);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
        return recipe.ToDetail();
    }

    private void EnsureCanEdit()
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null) throw new ForbiddenAccessException("Authentication is required.");
        if (!currentUser.IsAdmin && currentUser.UserId is null) throw new ForbiddenAccessException();
    }

    private async Task<string> CreateUniqueSlugAsync(string title, CancellationToken cancellationToken)
    {
        var baseSlug = RecipeCommandSlug.Slugify(title);
        var slug = baseSlug;
        var suffix = 2;
        while (await recipes.Query.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug, cancellationToken))
            slug = $"{baseSlug}-{suffix++}";
        return slug;
    }

    internal static string Slugify(string value) => RecipeCommandSlug.Slugify(value);

    internal static void AddChildren(Recipe recipe, IReadOnlyList<RecipeIngredientInput>? ingredients,
        IReadOnlyList<RecipeStepInput>? steps, RecipeNutritionInput? nutrition)
    {
        foreach (var item in ingredients ?? []) recipe.Ingredients.Add(new RecipeIngredient
        {
            Name = item.Name.Trim(), Quantity = item.Quantity, Unit = item.Unit?.Trim(), Notes = item.Notes?.Trim(), OrderIndex = item.OrderIndex
        });
        var number = 1;
        foreach (var item in steps ?? []) recipe.Steps.Add(new RecipeStep
        {
            StepNumber = number++, Title = item.Title?.Trim(), Description = item.Description.Trim(),
            DurationMinutes = item.DurationMinutes, ImageUrl = item.ImageUrl?.Trim()
        });
        if (nutrition is not null) recipe.Nutrition = new RecipeNutrition
        {
            Calories = nutrition.Calories, Protein = nutrition.Protein, Carbohydrates = nutrition.Carbohydrates,
            Fat = nutrition.Fat, Fiber = nutrition.Fiber, Sodium = nutrition.Sodium
        };
    }
}

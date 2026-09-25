using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record RecipeIngredientInput(string Name, decimal? Quantity, string? Unit, string? Notes, int OrderIndex = 0);
public sealed record RecipeStepInput(string? Title, string Description, int? DurationMinutes, string? ImageUrl);
public sealed record RecipeNutritionInput(int Calories, decimal Protein, decimal Carbohydrates, decimal Fat, decimal Fiber, decimal Sodium);

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

public sealed class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200);
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.PrepTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CookTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.Difficulty).NotEmpty().MaximumLength(20);
        RuleForEach(x => x.Ingredients).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.Quantity).GreaterThan(0).When(i => i.Quantity.HasValue);
            item.RuleFor(i => i.Notes).MaximumLength(200);
        });
        RuleForEach(x => x.Steps).ChildRules(item =>
        {
            item.RuleFor(i => i.Title).MaximumLength(200);
            item.RuleFor(i => i.Description).NotEmpty();
            item.RuleFor(i => i.DurationMinutes).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateRecipeCommandHandler : IRequestHandler<CreateRecipeCommand, RecipeDetailDto>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;

    public CreateRecipeCommandHandler(IApplicationDbContext context, ICurrentUserService currentUser, IRecipeCache cache)
    {
        this.context = context;
        this.currentUser = currentUser;
        this.cache = cache;
    }

    public async Task<RecipeDetailDto> Handle(CreateRecipeCommand request, CancellationToken cancellationToken)
    {
        EnsureCanEdit();
        if (!await context.Categories.AnyAsync(x => x.Id == request.CategoryId, cancellationToken))
            throw new NotFoundException(nameof(Category), request.CategoryId);

        var slug = await CreateUniqueSlugAsync(request.Title, cancellationToken);
        var recipe = new Recipe
        {
            Title = request.Title.Trim(), Slug = slug, Description = request.Description?.Trim(),
            CategoryId = request.CategoryId, AuthorId = currentUser.UserId!,
            PrepTimeMinutes = request.PrepTimeMinutes, CookTimeMinutes = request.CookTimeMinutes,
            Servings = request.Servings, Difficulty = request.Difficulty.Trim(), Status = RecipeStatus.Draft
        };
        AddChildren(recipe, request.Ingredients, request.Steps, request.Nutrition);
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync(cancellationToken);
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
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var suffix = 2;
        while (await context.Recipes.IgnoreQueryFilters().AnyAsync(x => x.Slug == slug, cancellationToken))
            slug = $"{baseSlug}-{suffix++}";
        return slug;
    }

    internal static string Slugify(string value) => new string(value.Trim().ToLowerInvariant()
        .Normalize(global::System.Text.NormalizationForm.FormD)
        .Where(x => global::System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x) != global::System.Globalization.UnicodeCategory.NonSpacingMark)
        .ToArray()).Replace('đ', 'd').Replace(' ', '-');

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

public sealed class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Title).NotEmpty().Length(5, 200);
        RuleFor(x => x.CategoryId).NotEmpty(); RuleFor(x => x.Servings).GreaterThan(0);
        RuleFor(x => x.PrepTimeMinutes).GreaterThanOrEqualTo(0); RuleFor(x => x.CookTimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Difficulty).NotEmpty().MaximumLength(20); RuleFor(x => x.RowVersion).NotEmpty();
        RuleForEach(x => x.Ingredients).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty().MaximumLength(100);
            item.RuleFor(i => i.Quantity).GreaterThan(0).When(i => i.Quantity.HasValue);
            item.RuleFor(i => i.Notes).MaximumLength(200);
        });
        RuleForEach(x => x.Steps).ChildRules(item =>
        {
            item.RuleFor(i => i.Title).MaximumLength(200);
            item.RuleFor(i => i.Description).NotEmpty();
            item.RuleFor(i => i.DurationMinutes).GreaterThanOrEqualTo(0);
        });
    }
}

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
        recipe.Servings = request.Servings; recipe.Difficulty = request.Difficulty.Trim();
        recipe.Slug = CreateRecipeCommandHandler.Slugify(recipe.Title);
        foreach (var ingredient in recipe.Ingredients) ingredient.IsDeleted = true;
        foreach (var step in recipe.Steps) step.IsDeleted = true;
        recipe.Ingredients.Clear(); recipe.Steps.Clear(); recipe.Nutrition = null;
        CreateRecipeCommandHandler.AddChildren(recipe, request.Ingredients, request.Steps, request.Nutrition);
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

public sealed record PublishRecipeCommand(Guid Id) : IRequest;
public sealed record ArchiveRecipeCommand(Guid Id) : IRequest;
public sealed record DeleteRecipeCommand(Guid Id) : IRequest;

public sealed class RecipeLifecycleHandler :
    IRequestHandler<PublishRecipeCommand>, IRequestHandler<ArchiveRecipeCommand>, IRequestHandler<DeleteRecipeCommand>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;
    public RecipeLifecycleHandler(IApplicationDbContext context, ICurrentUserService currentUser, IRecipeCache cache)
    { this.context = context; this.currentUser = currentUser; this.cache = cache; }

    public Task Handle(PublishRecipeCommand request, CancellationToken ct) => ChangeStatus(request.Id, RecipeStatus.Published, ct, true);
    public Task Handle(ArchiveRecipeCommand request, CancellationToken ct) => ChangeStatus(request.Id, RecipeStatus.Archived, ct, false);
    public async Task Handle(DeleteRecipeCommand request, CancellationToken ct)
    {
        var recipe = await GetOwnedRecipe(request.Id, ct); context.Recipes.Remove(recipe); await context.SaveChangesAsync(ct);
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", ct); await cache.RemoveByPrefixAsync("recipes:", ct);
    }

    private async Task ChangeStatus(Guid id, RecipeStatus status, CancellationToken ct, bool requireChildren)
    {
        var recipe = await GetOwnedRecipe(id, ct);
        if (requireChildren && (recipe.Steps.Count == 0 || recipe.Ingredients.Count == 0))
            throw new InvalidOperationException("A recipe must have at least one step and one ingredient before publishing.");
        recipe.Status = status; await context.SaveChangesAsync(ct);
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", ct); await cache.RemoveByPrefixAsync("recipes:", ct);
    }

    private async Task<Recipe> GetOwnedRecipe(Guid id, CancellationToken ct)
    {
        var recipe = await context.Recipes.Include(x => x.Steps).Include(x => x.Ingredients).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Recipe), id);
        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId) throw new ForbiddenAccessException();
        return recipe;
    }
}

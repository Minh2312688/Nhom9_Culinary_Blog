using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record AddRecipeIngredientCommand(
    Guid RecipeId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex) : IRequest<RecipeIngredientDto>;

public sealed class AddRecipeIngredientCommandValidator : AbstractValidator<AddRecipeIngredientCommand>
{
    public AddRecipeIngredientCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).Must(x => x is null or > 0).WithMessage("Quantity must be greater than zero when provided.");
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.OrderIndex).NotNull().GreaterThanOrEqualTo(0);
    }
}

public sealed class AddRecipeIngredientCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<AddRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(AddRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeIngredientCommandHelpers.GetEditableRecipeAsync(context, currentUser, request.RecipeId, cancellationToken);
        var ingredient = new RecipeIngredient
        {
            RecipeId = recipe.Id,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = RecipeIngredientCommandHelpers.TrimToNull(request.Unit),
            Notes = RecipeIngredientCommandHelpers.TrimToNull(request.Notes),
            OrderIndex = request.OrderIndex!.Value
        };
        context.RecipeIngredients.Add(ingredient);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeIngredientCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
        return ingredient.ToIngredientDto();
    }
}

public sealed record UpdateRecipeIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex) : IRequest<RecipeIngredientDto>;

public sealed class UpdateRecipeIngredientCommandValidator : AbstractValidator<UpdateRecipeIngredientCommand>
{
    public UpdateRecipeIngredientCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.IngredientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).Must(x => x is null or > 0).WithMessage("Quantity must be greater than zero when provided.");
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.OrderIndex).NotNull().GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateRecipeIngredientCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<UpdateRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(UpdateRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeIngredientCommandHelpers.GetEditableRecipeAsync(context, currentUser, request.RecipeId, cancellationToken);
        var ingredient = await context.RecipeIngredients.SingleOrDefaultAsync(
            x => x.Id == request.IngredientId && x.RecipeId == recipe.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(RecipeIngredient), request.IngredientId);

        ingredient.Name = request.Name.Trim();
        ingredient.Quantity = request.Quantity;
        ingredient.Unit = RecipeIngredientCommandHelpers.TrimToNull(request.Unit);
        ingredient.Notes = RecipeIngredientCommandHelpers.TrimToNull(request.Notes);
        ingredient.OrderIndex = request.OrderIndex!.Value;
        await context.SaveChangesAsync(cancellationToken);
        await RecipeIngredientCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
        return ingredient.ToIngredientDto();
    }
}

public sealed record DeleteRecipeIngredientCommand(Guid RecipeId, Guid IngredientId) : IRequest;

public sealed class DeleteRecipeIngredientCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<DeleteRecipeIngredientCommand>
{
    public async Task Handle(DeleteRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeIngredientCommandHelpers.GetEditableRecipeAsync(context, currentUser, request.RecipeId, cancellationToken);
        var ingredient = await context.RecipeIngredients.SingleOrDefaultAsync(
            x => x.Id == request.IngredientId && x.RecipeId == recipe.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(RecipeIngredient), request.IngredientId);

        context.RecipeIngredients.Remove(ingredient);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeIngredientCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
    }
}

internal static class RecipeIngredientCommandHelpers
{
    public static async Task<Recipe> GetEditableRecipeAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null && !currentUser.IsAdmin)
            throw new ForbiddenAccessException("Authentication is required.");

        var recipe = await context.Recipes.SingleOrDefaultAsync(x => x.Id == recipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), recipeId);
        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId)
            throw new ForbiddenAccessException();
        return recipe;
    }

    public static async Task InvalidateAsync(IRecipeCache cache, string slug, CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"recipes:slug:{slug}", cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
    }

    public static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal static class RecipeIngredientDtoMapping
{
    public static RecipeIngredientDto ToIngredientDto(this RecipeIngredient ingredient) => new(
        ingredient.Id, ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.Notes, ingredient.OrderIndex);
}

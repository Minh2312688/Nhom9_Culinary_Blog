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
    int OrderIndex = 0) : IRequest<RecipeIngredientDto>;

public sealed class AddRecipeIngredientCommandValidator : AbstractValidator<AddRecipeIngredientCommand>
{
    public AddRecipeIngredientCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(9_999_999.999m).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddRecipeIngredientCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<AddRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(AddRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeChildCommandHelpers.GetEditableRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);

        var ingredient = new RecipeIngredient
        {
            RecipeId = recipe.Id,
            Name = request.Name.Trim(),
            Quantity = request.Quantity,
            Unit = RecipeChildCommandHelpers.TrimToNull(request.Unit),
            Notes = RecipeChildCommandHelpers.TrimToNull(request.Notes),
            OrderIndex = request.OrderIndex
        };

        context.RecipeIngredients.Add(ingredient);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeChildCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
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
    int OrderIndex) : IRequest<RecipeIngredientDto>;

public sealed class UpdateRecipeIngredientCommandValidator : AbstractValidator<UpdateRecipeIngredientCommand>
{
    public UpdateRecipeIngredientCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.IngredientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0).LessThanOrEqualTo(9_999_999.999m).When(x => x.Quantity.HasValue);
        RuleFor(x => x.Unit).MaximumLength(50);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.OrderIndex).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateRecipeIngredientCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache) : IRequestHandler<UpdateRecipeIngredientCommand, RecipeIngredientDto>
{
    public async Task<RecipeIngredientDto> Handle(UpdateRecipeIngredientCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeChildCommandHelpers.GetEditableRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);
        var ingredient = await context.RecipeIngredients.SingleOrDefaultAsync(
            x => x.Id == request.IngredientId && x.RecipeId == recipe.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(RecipeIngredient), request.IngredientId);

        ingredient.Name = request.Name.Trim();
        ingredient.Quantity = request.Quantity;
        ingredient.Unit = RecipeChildCommandHelpers.TrimToNull(request.Unit);
        ingredient.Notes = RecipeChildCommandHelpers.TrimToNull(request.Notes);
        ingredient.OrderIndex = request.OrderIndex;

        await context.SaveChangesAsync(cancellationToken);
        await RecipeChildCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
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
        var recipe = await RecipeChildCommandHelpers.GetEditableRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);
        var ingredient = await context.RecipeIngredients.SingleOrDefaultAsync(
            x => x.Id == request.IngredientId && x.RecipeId == recipe.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(RecipeIngredient), request.IngredientId);

        ingredient.IsDeleted = true;
        await context.SaveChangesAsync(cancellationToken);
        await RecipeChildCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
    }
}

internal static class RecipeIngredientMapping
{
    public static RecipeIngredientDto ToIngredientDto(this RecipeIngredient ingredient) => new(
        ingredient.Id, ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.Notes, ingredient.OrderIndex);
}

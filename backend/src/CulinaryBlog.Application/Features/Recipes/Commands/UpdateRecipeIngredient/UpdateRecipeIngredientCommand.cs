using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record UpdateRecipeIngredientCommand(
    Guid RecipeId,
    Guid IngredientId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex) : IRequest<RecipeIngredientDto>;

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

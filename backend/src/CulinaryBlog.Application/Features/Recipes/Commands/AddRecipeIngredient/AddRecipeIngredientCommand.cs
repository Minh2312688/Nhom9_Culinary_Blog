using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record AddRecipeIngredientCommand(
    Guid RecipeId,
    string Name,
    decimal? Quantity,
    string? Unit,
    string? Notes,
    int? OrderIndex) : IRequest<RecipeIngredientDto>;

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

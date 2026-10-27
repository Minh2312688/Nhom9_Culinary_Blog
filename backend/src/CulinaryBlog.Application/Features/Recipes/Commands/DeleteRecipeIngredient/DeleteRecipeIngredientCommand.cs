using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

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

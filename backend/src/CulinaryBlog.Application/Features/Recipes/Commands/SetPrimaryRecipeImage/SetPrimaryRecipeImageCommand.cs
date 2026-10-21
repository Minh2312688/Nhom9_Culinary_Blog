using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record SetPrimaryRecipeImageCommand(Guid RecipeId, Guid ImageId) : IRequest<RecipeImageDto>;

public sealed class SetPrimaryRecipeImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IRecipeCache cache)
    : IRequestHandler<SetPrimaryRecipeImageCommand, RecipeImageDto>
{
    public async Task<RecipeImageDto> Handle(
        SetPrimaryRecipeImageCommand request,
        CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.GetOwnedRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);
        var images = await context.RecipeImages
            .Where(image => image.RecipeId == recipe.Id)
            .ToListAsync(cancellationToken);
        var selected = images.SingleOrDefault(image => image.Id == request.ImageId)
            ?? throw new NotFoundException(nameof(RecipeImage), request.ImageId);

        foreach (var image in images) image.IsPrimary = image.Id == selected.Id;
        await context.SaveChangesAsync(cancellationToken);
        await RecipeImageAccess.InvalidateRecipeCacheAsync(cache, recipe, cancellationToken);

        return new RecipeImageDto(selected.Id, selected.OriginalUrl, selected.MediumUrl,
            selected.ThumbnailUrl, selected.AltText, selected.IsPrimary, selected.OrderIndex);
    }
}

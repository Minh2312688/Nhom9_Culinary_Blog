using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record DeleteRecipeImageCommand(Guid RecipeId, Guid ImageId) : IRequest;

public sealed class DeleteRecipeImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFileStorageService storage,
    IRecipeCache cache)
    : IRequestHandler<DeleteRecipeImageCommand>
{
    public async Task Handle(DeleteRecipeImageCommand request, CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.GetOwnedRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);
        var images = await context.RecipeImages
            .Where(image => image.RecipeId == recipe.Id)
            .OrderBy(image => image.OrderIndex)
            .ToListAsync(cancellationToken);
        var selected = images.SingleOrDefault(image => image.Id == request.ImageId)
            ?? throw new NotFoundException(nameof(RecipeImage), request.ImageId);

        if (selected.IsPrimary && images.Count == 1)
        {
            throw new ValidationException("The only primary image of a recipe cannot be deleted.");
        }

        if (selected.IsPrimary)
        {
            images.First(image => image.Id != selected.Id).IsPrimary = true;
        }

        await DeleteStoredObjectsAsync(selected, cancellationToken);
        context.RecipeImages.Remove(selected);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeImageAccess.InvalidateRecipeCacheAsync(cache, recipe, cancellationToken);
    }

    private async Task DeleteStoredObjectsAsync(RecipeImage selected, CancellationToken cancellationToken)
    {
        Exception? firstFailure = null;
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var url in new[] { selected.OriginalUrl, selected.MediumUrl, selected.ThumbnailUrl })
        {
            if (string.IsNullOrWhiteSpace(url) || !attempted.Add(url))
            {
                continue;
            }

            try
            {
                await storage.DeleteAsync(url, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                firstFailure ??= exception;
            }
        }

        if (firstFailure is not null)
        {
            throw firstFailure;
        }
    }
}

using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Recipes;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record UploadRecipeImageCommand(
    Guid RecipeId,
    Stream Content,
    string FileName,
    string? ContentType,
    long Length,
    string? AltText,
    bool IsPrimary = false) : IRequest<RecipeImageUploadDto>;

public sealed class UploadRecipeImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFileStorageService storage,
    IRecipeCache cache,
    IRecipeImageJobEnqueuer imageJobEnqueuer,
    ILogger<UploadRecipeImageCommandHandler> logger)
    : IRequestHandler<UploadRecipeImageCommand, RecipeImageUploadDto>
{
    private const int MaxImagesPerRecipe = 10;

    public async Task<RecipeImageUploadDto> Handle(
        UploadRecipeImageCommand request,
        CancellationToken cancellationToken)
    {
        var recipe = await RecipeImageAccess.GetOwnedRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);
        var images = await context.RecipeImages
            .Where(image => image.RecipeId == recipe.Id)
            .OrderBy(image => image.OrderIndex)
            .ToListAsync(cancellationToken);

        if (images.Count >= MaxImagesPerRecipe)
        {
            throw new ValidationException($"A recipe cannot have more than {MaxImagesPerRecipe} images.");
        }

        var uploaded = await storage.UploadAsync(new FileUploadRequest(
            request.Content,
            request.FileName,
            request.ContentType,
            request.Length,
            StorageFolders.ForRecipe(recipe.Id)), cancellationToken);

        var isPrimary = request.IsPrimary || images.Count == 0;
        if (isPrimary)
        {
            foreach (var existing in images) existing.IsPrimary = false;
        }

        var image = new RecipeImage
        {
            RecipeId = recipe.Id,
            OriginalUrl = uploaded.Url,
            AltText = request.AltText?.Trim(),
            IsPrimary = isPrimary,
            OrderIndex = images.Count == 0 ? 0 : images.Max(existing => existing.OrderIndex) + 1
        };
        context.RecipeImages.Add(image);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await storage.DeleteAsync(uploaded.Url, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                logger.LogError(cleanupException,
                    "Failed to remove uploaded recipe image {ImageUrl} after database save failed.", uploaded.Url);
            }

            throw;
        }

        await RecipeImageAccess.InvalidateRecipeCacheAsync(cache, recipe, cancellationToken);
        try
        {
            await imageJobEnqueuer.EnqueueAsync(image.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception,
                "Recipe image {ImageId} was saved, but variant processing could not be enqueued.",
                image.Id);
        }

        return new RecipeImageUploadDto(image.Id, image.OriginalUrl, image.AltText, image.IsPrimary);
    }
}

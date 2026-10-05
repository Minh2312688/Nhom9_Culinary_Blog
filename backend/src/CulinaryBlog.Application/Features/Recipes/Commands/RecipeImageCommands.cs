using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
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

public sealed class UploadRecipeImageCommandValidator : AbstractValidator<UploadRecipeImageCommand>
{
    public UploadRecipeImageCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.Content).NotNull();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AltText).MaximumLength(200);
    }
}

public sealed record SetPrimaryRecipeImageCommand(Guid RecipeId, Guid ImageId) : IRequest<RecipeImageDto>;
public sealed record DeleteRecipeImageCommand(Guid RecipeId, Guid ImageId) : IRequest;

public sealed class UploadRecipeImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFileStorageService storage,
    IRecipeCache cache,
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
        return new RecipeImageUploadDto(image.Id, image.OriginalUrl, image.AltText, image.IsPrimary);
    }
}

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

        await storage.DeleteAsync(selected.OriginalUrl, cancellationToken);
        context.RecipeImages.Remove(selected);
        await context.SaveChangesAsync(cancellationToken);
        await RecipeImageAccess.InvalidateRecipeCacheAsync(cache, recipe, cancellationToken);
    }
}

internal static class RecipeImageAccess
{
    public static async Task<Recipe> GetOwnedRecipeAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid recipeId,
        CancellationToken cancellationToken)
    {
        var recipe = await context.Recipes.SingleOrDefaultAsync(
            item => item.Id == recipeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Recipe), recipeId);

        if (!currentUser.IsAdmin &&
            (!currentUser.IsAuthenticated || recipe.AuthorId != currentUser.UserId))
        {
            throw new ForbiddenAccessException();
        }

        return recipe;
    }

    public static async Task InvalidateRecipeCacheAsync(
        IRecipeCache cache,
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", cancellationToken);
        await cache.RemoveByPrefixAsync("recipes:", cancellationToken);
    }
}
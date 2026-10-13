using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Domain.Entities;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Images;

[Queue("image-processing")]
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail, LogEvents = true)]
public sealed class RecipeImageProcessingJob
{
    private readonly IApplicationDbContext context;
    private readonly IFileStorageService storage;
    private readonly IRecipeCache cache;
    private readonly RecipeImageProcessor processor;
    private readonly ILogger<RecipeImageProcessingJob> logger;

    public RecipeImageProcessingJob(
        IApplicationDbContext context,
        IFileStorageService storage,
        IRecipeCache cache,
        RecipeImageProcessor processor,
        ILogger<RecipeImageProcessingJob> logger)
    {
        this.context = context;
        this.storage = storage;
        this.cache = cache;
        this.processor = processor;
        this.logger = logger;
    }

    public async Task ProcessAsync(Guid imageId, CancellationToken cancellationToken)
    {
        if (imageId == Guid.Empty)
        {
            throw new ArgumentException("Recipe image ID is required.", nameof(imageId));
        }

        var image = await context.RecipeImages
            .Include(item => item.Recipe)
            .SingleOrDefaultAsync(item => item.Id == imageId, cancellationToken);
        if (image is null)
        {
            throw new InvalidOperationException($"Recipe image {imageId} was not found.");
        }

        FileUploadResult? thumbnail = null;
        FileUploadResult? medium = null;
        var variantsPersisted = false;
        try
        {
            await using var original = await storage.DownloadAsync(image.OriginalUrl, cancellationToken);
            var variants = await processor.CreateVariantsAsync(original, cancellationToken);

            if (variants.Status == RecipeImageVariantStatus.Unsupported)
            {
                image.VariantStatus = RecipeImageVariantStatus.Unsupported;
                await context.SaveChangesAsync(cancellationToken);
                await InvalidateRecipeCacheAsync(image.Recipe.Slug, cancellationToken);
                logger.LogInformation(
                    "Recipe image {ImageId} is unsupported for variant generation; original retained.",
                    imageId);
                return;
            }

            var folder = StorageFolders.ForRecipe(image.RecipeId);
            await using var thumbnailStream = new MemoryStream(variants.Thumbnail, writable: false);
            thumbnail = await storage.UploadAsync(
                new FileUploadRequest(
                    thumbnailStream,
                    $"thumbnail{variants.Extension}",
                    variants.ContentType,
                    variants.Thumbnail.Length,
                    folder),
                cancellationToken);

            await using var mediumStream = new MemoryStream(variants.Medium, writable: false);
            medium = await storage.UploadAsync(
                new FileUploadRequest(
                    mediumStream,
                    $"medium{variants.Extension}",
                    variants.ContentType,
                    variants.Medium.Length,
                    folder),
                cancellationToken);

            image.ThumbnailUrl = thumbnail.Url;
            image.MediumUrl = medium.Url;
            image.VariantStatus = RecipeImageVariantStatus.Ready;
            await context.SaveChangesAsync(cancellationToken);
            variantsPersisted = true;
            await InvalidateRecipeCacheAsync(image.Recipe.Slug, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (!variantsPersisted)
            {
                await CleanupVariantAsync(medium, imageId);
                await CleanupVariantAsync(thumbnail, imageId);
            }

            logger.LogError(exception, "Recipe image variant processing failed for image {ImageId}.", imageId);
            throw;
        }
    }

    private Task InvalidateRecipeCacheAsync(string slug, CancellationToken cancellationToken)
        => cache.RemoveAsync($"recipes:slug:{slug.ToLowerInvariant()}", cancellationToken);

    private async Task CleanupVariantAsync(FileUploadResult? variant, Guid imageId)
    {
        if (variant is null)
        {
            return;
        }

        try
        {
            await storage.DeleteAsync(variant.Url, CancellationToken.None);
        }
        catch (Exception cleanupException)
        {
            logger.LogWarning(cleanupException,
                "Failed to remove temporary variant for recipe image {ImageId}.", imageId);
        }
    }
}

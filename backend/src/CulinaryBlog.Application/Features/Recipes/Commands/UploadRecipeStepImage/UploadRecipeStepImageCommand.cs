using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed record UploadRecipeStepImageCommand(
    Guid RecipeId,
    Guid StepId,
    Stream Content,
    string FileName,
    string? ContentType,
    long Length) : IRequest<RecipeStepDto>;

public sealed class UploadRecipeStepImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IFileStorageService storage,
    IRecipeCache cache,
    ILogger<UploadRecipeStepImageCommandHandler> logger)
    : IRequestHandler<UploadRecipeStepImageCommand, RecipeStepDto>
{
    public async Task<RecipeStepDto> Handle(
        UploadRecipeStepImageCommand request,
        CancellationToken cancellationToken)
    {
        var recipe = await RecipeStepCommandHelpers.GetEditableRecipeAsync(
            context, currentUser, request.RecipeId, cancellationToken);
        var step = await context.RecipeSteps.SingleOrDefaultAsync(
            item => item.Id == request.StepId && item.RecipeId == recipe.Id,
            cancellationToken)
            ?? throw new NotFoundException(nameof(RecipeStep), request.StepId);

        var uploaded = await storage.UploadAsync(new FileUploadRequest(
            request.Content,
            request.FileName,
            request.ContentType,
            request.Length,
            StorageFolders.ForRecipe(recipe.Id)), cancellationToken);
        var previousImageUrl = step.ImageUrl;
        step.ImageUrl = uploaded.Url;

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
                    "Failed to remove uploaded step image {ImageUrl} after database save failed.",
                    uploaded.Url);
            }

            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousImageUrl))
        {
            try
            {
                await storage.DeleteAsync(previousImageUrl, cancellationToken);
            }
            catch (Exception cleanupException) when (cleanupException is not OperationCanceledException)
            {
                logger.LogError(cleanupException,
                    "Failed to remove replaced step image {ImageUrl}.",
                    previousImageUrl);
            }
        }

        await RecipeStepCommandHelpers.InvalidateAsync(cache, recipe.Slug, cancellationToken);
        return step.ToStepDto();
    }
}

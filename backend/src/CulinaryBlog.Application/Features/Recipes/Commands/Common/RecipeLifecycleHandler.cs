using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Commands;

public sealed class RecipeLifecycleHandler :
    IRequestHandler<PublishRecipeCommand>,
    IRequestHandler<ArchiveRecipeCommand>,
    IRequestHandler<DeleteRecipeCommand>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;
    private readonly IFileStorageService? storage;

    public RecipeLifecycleHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRecipeCache cache,
        IFileStorageService? storage = null)
    {
        this.context = context;
        this.currentUser = currentUser;
        this.cache = cache;
        this.storage = storage;
    }

    public Task Handle(PublishRecipeCommand request, CancellationToken ct) => ChangeStatus(request.Id, RecipeStatus.Published, ct, true);
    public Task Handle(ArchiveRecipeCommand request, CancellationToken ct) => ChangeStatus(request.Id, RecipeStatus.Archived, ct, false);

    public async Task Handle(DeleteRecipeCommand request, CancellationToken ct)
    {
        var recipe = await GetOwnedRecipe(request.Id, ct);

        // Delete all associated image objects from MinIO storage (FR-RCP-007 / TECH-RISK-004)
        if (storage is not null && recipe.Images.Count > 0)
        {
            var urls = recipe.Images
                .SelectMany(img => new[] { img.OriginalUrl, img.MediumUrl, img.ThumbnailUrl })
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Distinct(StringComparer.Ordinal);

            foreach (var url in urls)
            {
                try
                {
                    await storage.DeleteAsync(url!, ct);
                }
                catch (Exception)
                {
                    // Best-effort cleanup to prevent blocking database cascade deletion
                }
            }
        }

        context.Recipes.Remove(recipe);
        await context.SaveChangesAsync(ct);
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug.ToLowerInvariant()}", ct);
        await cache.RemoveByPrefixAsync("recipes:", ct);
    }

    private async Task ChangeStatus(Guid id, RecipeStatus status, CancellationToken ct, bool requireChildren)
    {
        var recipe = await GetOwnedRecipe(id, ct);
        if (requireChildren) recipe.Publish(DateTimeOffset.UtcNow);
        else recipe.Status = status;
        await context.SaveChangesAsync(ct);
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug.ToLowerInvariant()}", ct);
        await cache.RemoveByPrefixAsync("recipes:", ct);
    }

    private async Task<Recipe> GetOwnedRecipe(Guid id, CancellationToken ct)
    {
        var recipe = await context.Recipes
            .Include(x => x.Steps)
            .Include(x => x.Ingredients)
            .Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Recipe), id);
        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId)
            throw new ForbiddenAccessException();
        return recipe;
    }
}

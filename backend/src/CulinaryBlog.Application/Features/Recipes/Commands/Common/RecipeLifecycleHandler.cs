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
    private readonly IFileStorageService storage;

    public RecipeLifecycleHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRecipeCache cache,
        IFileStorageService storage)
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
        // Soft-delete toàn bộ hệ thống: EF không gửi DELETE xuống DB,
        // do đó phải Remove cả recipe VÀ RemoveRange ảnh trong một SaveChangesAsync duy nhất.
        var recipe = await GetOwnedRecipe(request.Id, ct);
        var images = await context.RecipeImages
            .Where(image => image.RecipeId == recipe.Id)
            .ToListAsync(ct);

        // Bước 1: xóa storage objects trước (original/medium/thumbnail).
        // Nếu bất kỳ object nào fail → throw, KHÔNG soft-delete recipe.
        await DeleteStoredObjectsAsync(images, recipe, ct);

        // Bước 2: soft-delete recipe + images cùng một SaveChangesAsync.
        // Không dùng Remove (EF InMemory xóa khỏi store ngay, soft-delete override
        // sau đó không thể UPDATE được). Set IsDeleted + Modified trực tiếp.
        recipe.IsDeleted = true;
        recipe.UpdatedAt = DateTime.UtcNow;
        context.Entry(recipe).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        foreach (var image in images)
        {
            image.IsDeleted = true;
            image.UpdatedAt = DateTime.UtcNow;
            context.Entry(image).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        }
        await context-saveChangesAsync(ct);

        // Bước 3: invalidate cache sau khi DB đã lưu.
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", ct);
        await cache.RemoveByPrefixAsync("recipes:", ct);
    }

    private async Task DeleteStoredObjectsAsync(List<RecipeImage> images, Recipe recipe, CancellationToken ct)
    {
        Exception? firstFailure = null;
        var attempted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in images)
        {
            foreach (var url in new[] { image.OriginalUrl, image.MediumUrl, image.ThumbnailUrl })
            {
                if (string.IsNullOrEmpty(url) || !attempted.Add(url))
                {
                    continue;
                }

                try
                {
                    await storage.DeleteAsync(url, ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    firstFailure ??= exception;
                }
            }
        }

        if (firstFailure is not null)
        {
            // Invalidate cache trước khi throw (không Remove/SaveChanges).
            await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", ct);
            await cache.RemoveByPrefixAsync("recipes:", ct);
            throw firstFailure;
        }
    }

    private async Task ChangeStatus(Guid id, RecipeStatus status, CancellationToken ct, bool requireChildren)
    {
        var recipe = await GetOwnedRecipe(id, ct);
        if (requireChildren) recipe.Publish(DateTimeOffset.UtcNow);
        else recipe.Status = status;
        await context.SaveChangesAsync(ct);
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", ct);
        await cache.RemoveByPrefixAsync("recipes:", ct);
    }

    private async Task<Recipe> GetOwnedRecipe(Guid id, CancellationToken ct)
    {
        var recipe = await context.Recipes.Include(x => x.Steps).Include(x => x.Ingredients)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException(nameof(Recipe), id);
        if (!currentUser.IsAdmin && recipe.AuthorId != currentUser.UserId)
            throw new ForbiddenAccessException();
        return recipe;
    }
}
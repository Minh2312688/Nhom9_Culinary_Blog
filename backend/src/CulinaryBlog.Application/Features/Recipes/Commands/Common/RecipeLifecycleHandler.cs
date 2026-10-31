using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
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

    public RecipeLifecycleHandler(IApplicationDbContext context, ICurrentUserService currentUser, IRecipeCache cache)
    {
        this.context = context;
        this.currentUser = currentUser;
        this.cache = cache;
    }

    public Task Handle(PublishRecipeCommand request, CancellationToken ct) => ChangeStatus(request.Id, RecipeStatus.Published, ct, true);
    public Task Handle(ArchiveRecipeCommand request, CancellationToken ct) => ChangeStatus(request.Id, RecipeStatus.Archived, ct, false);
    public async Task Handle(DeleteRecipeCommand request, CancellationToken ct)
    {
        var recipe = await GetOwnedRecipe(request.Id, ct);
        context.Recipes.Remove(recipe);
        await context.SaveChangesAsync(ct);
        await cache.RemoveAsync($"recipes:slug:{recipe.Slug}", ct);
        await cache.RemoveByPrefixAsync("recipes:", ct);
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

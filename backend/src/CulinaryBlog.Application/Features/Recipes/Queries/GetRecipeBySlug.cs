using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Authentication;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Recipes.Queries;

public sealed record GetRecipeBySlugQuery(string Slug) : IRequest<RecipeDetailDto>;

public sealed class GetRecipeBySlugQueryValidator : AbstractValidator<GetRecipeBySlugQuery>
{
    public GetRecipeBySlugQueryValidator() => RuleFor(x => x.Slug).NotEmpty().MaximumLength(250);
}

public sealed class GetRecipeBySlugQueryHandler : IRequestHandler<GetRecipeBySlugQuery, RecipeDetailDto>
{
    private readonly IRecipeRepository recipes;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;
    private readonly IIdentityService identityService;

    public GetRecipeBySlugQueryHandler(
        IRecipeRepository recipes,
        ICurrentUserService currentUser,
        IRecipeCache cache,
        IIdentityService identityService)
    {
        this.recipes = recipes;
        this.currentUser = currentUser;
        this.cache = cache;
        this.identityService = identityService;
    }

    public async Task<RecipeDetailDto> Handle(GetRecipeBySlugQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"recipes:slug:{request.Slug.ToLowerInvariant()}";
        var cached = await cache.GetAsync<RecipeDetailDto>(cacheKey, cancellationToken);
        if (cached is not null && CanView(cached.Status, cached.AuthorId))
            return await IncludePublicAuthorAsync(cached with { Author = null }, cancellationToken);

        var recipe = await recipes.Query.AsNoTracking()
            .Include(x => x.Ingredients)
            .Include(x => x.Steps)
            .Include(x => x.Images)
            .Include(x => x.Nutrition)
            .SingleOrDefaultAsync(x => x.Slug == request.Slug, cancellationToken);

        if (recipe is null) throw new NotFoundException(nameof(Recipe), request.Slug);
        if (!CanView(recipe.Status, recipe.AuthorId)) throw new ForbiddenAccessException();

        var result = recipe.ToDetail();
        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return await IncludePublicAuthorAsync(result, cancellationToken);
    }

    private async Task<RecipeDetailDto> IncludePublicAuthorAsync(
        RecipeDetailDto recipe,
        CancellationToken cancellationToken)
    {
        var author = await identityService.GetUserProfileByIdAsync(recipe.AuthorId, cancellationToken);
        if (author is not { Succeeded: true, IsActive: true, UserId: not null, DisplayName: not null })
            return recipe;

        return recipe with
        {
            Author = new RecipeAuthorDto(author.UserId, author.DisplayName, author.AvatarUrl)
        };
    }

    private bool CanView(RecipeStatus status, string authorId) =>
        status == RecipeStatus.Published || currentUser.IsAdmin || currentUser.UserId == authorId;
}

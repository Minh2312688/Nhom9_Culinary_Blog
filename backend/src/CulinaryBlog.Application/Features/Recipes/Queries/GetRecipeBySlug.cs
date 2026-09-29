using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
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
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;

    public GetRecipeBySlugQueryHandler(IApplicationDbContext context, ICurrentUserService currentUser, IRecipeCache cache)
    {
        this.context = context;
        this.currentUser = currentUser;
        this.cache = cache;
    }

    public async Task<RecipeDetailDto> Handle(GetRecipeBySlugQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"recipes:slug:{request.Slug.ToLowerInvariant()}";
        var cached = await cache.GetAsync<RecipeDetailDto>(cacheKey, cancellationToken);
        if (cached is not null && CanView(cached.Status, cached.AuthorId)) return cached;

        var recipe = await context.Recipes.AsNoTracking()
            .Include(x => x.Ingredients)
            .Include(x => x.Steps)
            .Include(x => x.Images)
            .Include(x => x.Nutrition)
            .SingleOrDefaultAsync(x => x.Slug == request.Slug, cancellationToken);

        if (recipe is null) throw new NotFoundException(nameof(Recipe), request.Slug);
        if (!CanView(recipe.Status, recipe.AuthorId)) throw new ForbiddenAccessException();

        var result = recipe.ToDetail();
        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }

    private bool CanView(RecipeStatus status, string authorId) =>
        status == RecipeStatus.Published || currentUser.IsAdmin || currentUser.UserId == authorId;
}

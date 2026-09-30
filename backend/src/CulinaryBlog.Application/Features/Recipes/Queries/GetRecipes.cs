using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CulinaryBlog.Application.Features.Recipes.Queries;

public sealed record GetRecipesQuery(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string SortBy = "createdAt",
    string SortOrder = "desc",
    string? Search = null) : IRequest<PaginatedResult<RecipeSummaryDto>>;

public sealed class GetRecipesQueryValidator : AbstractValidator<GetRecipesQuery>
{
    private static readonly string[] SortFields = ["title", "createdAt", "cookTime", "prepTime"];

    public GetRecipesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.MaxCookTime).GreaterThanOrEqualTo(0).When(x => x.MaxCookTime.HasValue);
        RuleFor(x => x.MinServings).GreaterThan(0).When(x => x.MinServings.HasValue);
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);
        RuleFor(x => x.SortBy).Must(value => SortFields.Contains(value.ToLowerInvariant()))
            .WithMessage("sortBy must be title, createdAt, cookTime, or prepTime.");
        RuleFor(x => x.SortOrder).Must(value => value.Equals("asc", StringComparison.OrdinalIgnoreCase) || value.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("sortOrder must be asc or desc.");
    }
}

public sealed class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PaginatedResult<RecipeSummaryDto>>
{
    private readonly IRecipeRepository recipes;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache cache;

    public GetRecipesQueryHandler(IRecipeRepository recipes, ICurrentUserService currentUser, IRecipeCache cache)
    {
        this.recipes = recipes;
        this.currentUser = currentUser;
        this.cache = cache;
    }

    public async Task<PaginatedResult<RecipeSummaryDto>> Handle(GetRecipesQuery request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var terms = Regex.Matches(request.Search, @"[\p{L}\p{N}]+")
                .Select(match => match.Value)
                .ToArray();

            if (terms.Length > 0)
            {
                var normalized = string.Join(' ', terms).ToLowerInvariant();
                var visibility = currentUser.IsAdmin ? "admin" : currentUser.UserId ?? "anonymous";
                var cacheKey = $"recipes:search:v1:{visibility}:{request.Page}:{request.PageSize}:{normalized}";
                var cached = await cache.GetAsync<PaginatedResult<RecipeSummaryDto>>(cacheKey, cancellationToken);
                if (cached is not null) return cached;

                var result = await recipes.SearchAsync(normalized, currentUser.UserId, currentUser.IsAdmin,
                    request.Page, request.PageSize, cancellationToken);
                await cache.SetAsync(cacheKey, result, TimeSpan.FromSeconds(60), cancellationToken);
                return result;
            }
        }

        var query = recipes.Query.AsNoTracking();
        query = currentUser.IsAdmin
            ? query
            : query.Where(x => x.Status == RecipeStatus.Published ||
                (currentUser.UserId != null && x.AuthorId == currentUser.UserId));

        if (request.CategoryId.HasValue) query = query.Where(x => x.CategoryId == request.CategoryId);
        if (!string.IsNullOrWhiteSpace(request.Difficulty)) query = query.Where(x => x.Difficulty == request.Difficulty);
        if (request.MaxCookTime.HasValue) query = query.Where(x => x.CookTimeMinutes <= request.MaxCookTime);
        if (request.MinServings.HasValue) query = query.Where(x => x.Servings >= request.MinServings);

        var descending = request.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy.ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "cooktime" => descending ? query.OrderByDescending(x => x.CookTimeMinutes) : query.OrderBy(x => x.CookTimeMinutes),
            "preptime" => descending ? query.OrderByDescending(x => x.PrepTimeMinutes) : query.OrderBy(x => x.PrepTimeMinutes),
            _ => descending ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new RecipeSummaryDto(x.Id, x.Title, x.Slug, x.Description, x.Difficulty,
                x.CookTimeMinutes, x.Servings, x.Status, x.CategoryId, x.AuthorId, x.RowVersion))
            .ToListAsync(cancellationToken);

        return new PaginatedResult<RecipeSummaryDto>(items, totalCount, request.Page, request.PageSize);
    }
}

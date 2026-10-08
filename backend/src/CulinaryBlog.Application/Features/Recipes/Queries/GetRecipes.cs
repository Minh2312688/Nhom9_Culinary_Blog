using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace CulinaryBlog.Application.Features.Recipes.Queries;

public sealed record GetRecipesQuery(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string? SortBy = null,
    string? SortOrder = null,
    string? Search = null) : IRequest<PaginatedResult<RecipeSummaryDto>>;

public sealed class GetRecipesQueryValidator : AbstractValidator<GetRecipesQuery>
{
    private static readonly string[] SortFields = ["title", "createdat", "cooktime", "preptime"];
    private static readonly string[] Difficulties = ["Easy", "Medium", "Hard"];

    public GetRecipesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
        RuleFor(x => x.MaxCookTime).GreaterThan(0).When(x => x.MaxCookTime.HasValue);
        RuleFor(x => x.MinServings).GreaterThan(0).When(x => x.MinServings.HasValue);
        RuleFor(x => x.Difficulty)
            .Must(value => Difficulties.Contains(value, StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.Difficulty))
            .WithMessage("difficulty must be Easy, Medium, or Hard.");
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);
        RuleFor(x => x.SortBy)
            .Must(value => value is not null && SortFields.Contains(value.ToLowerInvariant()))
            .When(x => x.SortBy is not null)
            .WithMessage("sortBy must be title, createdAt, cookTime, or prepTime.");
        RuleFor(x => x.SortOrder)
            .Must(value => value is not null && (value.Equals("asc", StringComparison.OrdinalIgnoreCase) || value.Equals("desc", StringComparison.OrdinalIgnoreCase)))
            .When(x => x.SortOrder is not null)
            .WithMessage("sortOrder must be asc or desc.");
        RuleFor(x => x.SortOrder)
            .Null()
            .When(x => x.SortBy is null)
            .WithMessage("sortBy is required when sortOrder is specified.");
    }
}

public sealed class GetRecipesQueryHandler : IRequestHandler<GetRecipesQuery, PaginatedResult<RecipeSummaryDto>>
{
    private static readonly TimeSpan PopularityWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SearchResultTtl = TimeSpan.FromSeconds(60);
    private const int PopularityThreshold = 3;
    private readonly IRecipeRepository recipes;
    private readonly ICurrentUserService currentUser;
    private readonly IRecipeCache? cache;

    public GetRecipesQueryHandler(IRecipeRepository recipes, ICurrentUserService currentUser, IRecipeCache? cache = null)
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
                var cacheable = request.CategoryId is null && request.Difficulty is null &&
                    request.MaxCookTime is null && request.MinServings is null;
                var cacheKey = BuildSearchCacheKey(normalized, request);
                if (cacheable && cache is not null)
                {
                    var cached = await cache.GetAsync<PaginatedResult<RecipeSummaryDto>>(cacheKey, cancellationToken);
                    if (cached is not null) return cached;
                    var popularityKey = $"recipes:search:popularity:{cacheKey.Substring("recipes:search:result:".Length)}";
                    var observed = await cache.GetAsync<int?>(popularityKey, cancellationToken) ?? 0;
                    var count = observed >= PopularityThreshold ? PopularityThreshold : observed + 1;
                    await cache.SetAsync(popularityKey, count, PopularityWindow, cancellationToken);
                    if (count >= PopularityThreshold)
                    {
                        var popularResult = await recipes.SearchAsync(normalized, currentUser.UserId, currentUser.IsAdmin,
                            request.Page, request.PageSize, request.CategoryId, NormalizeDifficulty(request.Difficulty),
                            request.MaxCookTime, request.MinServings, cancellationToken, request.SortBy, request.SortOrder);
                        await cache.SetAsync(cacheKey, popularResult, SearchResultTtl, cancellationToken);
                        return popularResult;
                    }
                }

                return await recipes.SearchAsync(normalized, currentUser.UserId, currentUser.IsAdmin,
                    request.Page, request.PageSize, request.CategoryId, NormalizeDifficulty(request.Difficulty),
                    request.MaxCookTime, request.MinServings, cancellationToken, request.SortBy, request.SortOrder);
            }
        }

        var query = recipes.Query.AsNoTracking();
        query = currentUser.IsAdmin
            ? query
            : query.Where(x => x.Status == RecipeStatus.Published ||
                (currentUser.UserId != null && x.AuthorId == currentUser.UserId));

        if (request.CategoryId.HasValue) query = query.Where(x => x.CategoryId == request.CategoryId);
        if (!string.IsNullOrWhiteSpace(request.Difficulty))
        {
            var difficulty = NormalizeDifficulty(request.Difficulty);
            if (difficulty.HasValue) query = query.Where(x => x.Difficulty == difficulty.Value);
        }
        if (request.MaxCookTime.HasValue) query = query.Where(x => x.CookTimeMinutes <= request.MaxCookTime);
        if (request.MinServings.HasValue) query = query.Where(x => x.Servings >= request.MinServings);

        var descending = (request.SortOrder ?? "desc").Equals("desc", StringComparison.OrdinalIgnoreCase);
        query = (request.SortBy ?? "createdAt").ToLowerInvariant() switch
        {
            "title" => descending ? query.OrderByDescending(x => x.Title).ThenBy(x => x.Id) : query.OrderBy(x => x.Title).ThenBy(x => x.Id),
            "cooktime" => descending ? query.OrderByDescending(x => x.CookTimeMinutes).ThenBy(x => x.Id) : query.OrderBy(x => x.CookTimeMinutes).ThenBy(x => x.Id),
            "preptime" => descending ? query.OrderByDescending(x => x.PrepTimeMinutes).ThenBy(x => x.Id) : query.OrderBy(x => x.PrepTimeMinutes).ThenBy(x => x.Id),
            "createdat" => descending ? query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id) : query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => throw new ArgumentException("Unsupported recipe sort field.", nameof(request.SortBy))
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);
        if (request.Page > totalPages)
            return new PaginatedResult<RecipeSummaryDto>(Array.Empty<RecipeSummaryDto>(), totalCount, request.Page, request.PageSize);

        var rows = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new
            {
                x.Id, x.Title, x.Slug, x.Description, x.Difficulty, x.CookTimeMinutes,
                x.Servings, x.Status, x.CategoryId, x.AuthorId, x.RowVersion
            })
            .ToListAsync(cancellationToken);
        var items = rows.Select(x => new RecipeSummaryDto(x.Id, x.Title, x.Slug, x.Description,
            x.Difficulty.ToString(), x.CookTimeMinutes, x.Servings, x.Status, x.CategoryId,
            x.AuthorId, x.RowVersion)).ToList();

        return new PaginatedResult<RecipeSummaryDto>(items, totalCount, request.Page, request.PageSize);
    }

    private static RecipeDifficulty? NormalizeDifficulty(string? difficulty) =>
        Enum.TryParse<RecipeDifficulty>(difficulty, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;

    private string BuildSearchCacheKey(string normalized, GetRecipesQuery request)
    {
        var scope = currentUser.IsAdmin ? "admin" : currentUser.UserId is null ? "guest" : $"user:{currentUser.UserId}";
        var material = $"{normalized}|{request.Page}|{request.PageSize}|{request.SortBy?.ToLowerInvariant() ?? "relevance"}|{request.SortOrder?.ToLowerInvariant() ?? "desc"}|{scope}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        return $"recipes:search:result:{hash}";
    }
}

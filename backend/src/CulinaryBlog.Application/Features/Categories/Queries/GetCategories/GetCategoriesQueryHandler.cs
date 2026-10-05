using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories;
using CulinaryBlog.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;

public sealed class GetCategoriesQueryHandler(
    IApplicationDbContext context,
    ICategoryCache cache) : IRequestHandler<GetCategoriesQuery, PaginatedResult<CategoryDto>>
{
    public async Task<PaginatedResult<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var key = CategoryCacheKeys.ForList(request);
        var cached = await cache.GetAsync<PaginatedResult<CategoryDto>>(key, cancellationToken);
        if (cached is not null)
        {
            var refreshedItems = new List<CategoryDto>(cached.Items.Count);
            foreach (var item in cached.Items)
            {
                var categoryId = await context.Categories.AsNoTracking()
                    .Where(category => category.Id == item.Id || category.Slug == item.Slug)
                    .Select(category => (Guid?)category.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                var count = categoryId.HasValue
                    ? await context.Recipes.CountAsync(recipe => recipe.CategoryId == categoryId.Value &&
                        recipe.Status == RecipeStatus.Published, cancellationToken)
                    : 0;
                refreshedItems.Add(item with { RecipeCount = count });
            }

            return new PaginatedResult<CategoryDto>(refreshedItems, cached.TotalCount, cached.Page, cached.PageSize);
        }

        var query = context.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(category => category.Name.ToLower().Contains(search) ||
                (category.Description != null && category.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        query = (request.SortBy.ToLowerInvariant(), request.Descending) switch
        {
            ("createdat", false) => query.OrderBy(category => category.CreatedAt),
            ("createdat", true) => query.OrderByDescending(category => category.CreatedAt),
            ("orderindex", false) => query.OrderBy(category => category.OrderIndex),
            ("orderindex", true) => query.OrderByDescending(category => category.OrderIndex),
            (_, true) => query.OrderByDescending(category => category.Name),
            _ => query.OrderBy(category => category.Name)
        };

        var items = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(category => new CategoryDto(category.Id, category.Name, category.Slug,
                category.Description, category.ImageUrl, category.OrderIndex, category.CreatedAt,
                category.Recipes.Count(recipe => !recipe.IsDeleted && recipe.Status == RecipeStatus.Published)))
            .ToListAsync(cancellationToken);

        var result = new PaginatedResult<CategoryDto>(items, totalCount, request.Page, request.PageSize);
        await cache.SetAsync(key, result, CategoryCacheKeys.Ttl, cancellationToken);
        return result;
    }
}

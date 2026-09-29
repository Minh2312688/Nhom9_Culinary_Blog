using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
public class GetCategoriesQueryHandler
 : IRequestHandler<GetCategoriesQuery, PaginatedResult<CategoryDto>>
{
 private readonly IApplicationDbContext _context;
 private readonly ICategoryCache _cache;
 public GetCategoriesQueryHandler(IApplicationDbContext context, ICategoryCache cache)
 {
 _context = context;
 _cache = cache;
 }
 public async Task<PaginatedResult<CategoryDto>> Handle(
 GetCategoriesQuery request,
 CancellationToken cancellationToken)
 {
 // Cache-aside: key đã chứa đầy đủ page/pageSize/search/sortBy/sortOrder
 var cacheKey = CategoryCacheKeys.ForList(request);
 var cached = await _cache.GetAsync<PaginatedResult<CategoryDto>>(cacheKey, cancellationToken);
 if (cached is not null)
 {
 var refreshedItems = await AttachRecipeCountsAsync(cached.Items, cancellationToken);
 return new PaginatedResult<CategoryDto>(
 refreshedItems, cached.TotalCount, cached.Page, cached.PageSize);
 }
 // AsNoTracking(): không cần tracking vì chỉ đọc dữ liệu (Query, không phải Command)
 var query = _context.Categories.AsNoTracking();
 // Áp dụng bộ lọc tìm kiếm — chỉ thêm điều kiện WHERE khi có giá trị
 if (!string.IsNullOrWhiteSpace(request.Search))
 {
 var search = request.Search.ToLower().Trim();
 query = query.Where(c =>
 c.Name.ToLower().Contains(search) ||
 (c.Description != null && c.Description.ToLower().Contains(search)));
 }
 // COUNT trước khi phân trang — đây là truy vấn SQL riêng biệt
 var totalCount = await query.CountAsync(cancellationToken);
 // Sắp xếp theo sortBy + sortOrder (CONFLICT-003); sortBy lạ rơi về Name tăng dần
 var descending = request.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
 query = request.SortBy.ToLowerInvariant() switch
 {
 "name" => descending ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
 "createdat" => descending
 ? query.OrderByDescending(c => c.CreatedAt)
 : query.OrderBy(c => c.CreatedAt),
 _ => query.OrderBy(c => c.Name) // default
 };
 // Phân trang: Skip bỏ qua các trang trước, Take lấy đúng số item cần
 // SQL tương đương: OFFSET (page-1)*pageSize ROWS FETCH NEXT pageSize ROWS ONLY
 var items = await query
 .Skip((request.Page - 1) * request.PageSize)
 .Take(request.PageSize)
 .ProjectToType<CategoryDto>() // Mapster projection: chỉ SELECT cột cần thiết
 .ToListAsync(cancellationToken);
 var itemsWithRecipeCount = await AttachRecipeCountsAsync(items, cancellationToken);
 var result = new PaginatedResult<CategoryDto>(
 itemsWithRecipeCount, totalCount, request.Page, request.PageSize);
 // Ghi cache sau khi đọc database, TTL 30 phút theo contract nhóm
 await _cache.SetAsync(cacheKey, result, CategoryCacheKeys.Ttl, cancellationToken);
 return result;
 }
 // Gắn recipeCount (FR-CAT-001) bằng MỘT truy vấn GROUP BY cho cả trang, không N+1.
 // Recipe Published mới được tính; Recipe đã soft delete bị global query filter của
 // RecipeConfiguration loại nên không cần IgnoreQueryFilters.
 private async Task<List<CategoryDto>> AttachRecipeCountsAsync(
 IReadOnlyList<CategoryDto> items,
 CancellationToken cancellationToken)
 {
 if (items.Count == 0)
 {
 return [];
 }
 var categoryIds = items.Select(item => item.Id).ToList();
 var publishedCounts = await _context.Recipes
 .Where(recipe => categoryIds.Contains(recipe.CategoryId) &&
 recipe.Status == RecipeStatus.Published)
 .GroupBy(recipe => recipe.CategoryId)
 .Select(group => new { CategoryId = group.Key, Count = group.Count() })
 .ToDictionaryAsync(
 entry => entry.CategoryId,
 entry => entry.Count,
 cancellationToken);
 return items
 .Select(item => item with
 {
 RecipeCount = publishedCounts.GetValueOrDefault(item.Id)
 })
 .ToList();
 }
}
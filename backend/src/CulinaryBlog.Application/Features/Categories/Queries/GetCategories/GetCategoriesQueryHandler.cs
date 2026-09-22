using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.DTOs;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
public class GetCategoriesQueryHandler
 : IRequestHandler<GetCategoriesQuery, PaginatedResult<CategoryDto>>
{
 private readonly IApplicationDbContext _context;
 public GetCategoriesQueryHandler(IApplicationDbContext context)
 => _context = context;
 public async Task<PaginatedResult<CategoryDto>> Handle(
 GetCategoriesQuery request,
 CancellationToken cancellationToken)
 {
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
 // Sắp xếp động dựa trên tham số request
 query = (request.SortBy.ToLower(), request.Descending) switch
 {
 ("name", false) => query.OrderBy(c => c.Name),
 ("name", true) => query.OrderByDescending(c => c.Name),
 ("createdat", false) => query.OrderBy(c => c.CreatedAt),
 ("createdat", true) => query.OrderByDescending(c => c.CreatedAt),
 _ => query.OrderBy(c => c.Name) // default
 };
 // Phân trang: Skip bỏ qua các trang trước, Take lấy đúng số item cần
 // SQL tương đương: OFFSET (page-1)*pageSize ROWS FETCH NEXT pageSize ROWS ONLY
 var items = await query
 .Skip((request.Page - 1) * request.PageSize)
 .Take(request.PageSize)
 .ProjectToType<CategoryDto>() // Mapster projection: chỉ SELECT cột cần thiết
 .ToListAsync(cancellationToken);
 return new PaginatedResult<CategoryDto>(
 items, totalCount, request.Page, request.PageSize);
 }
}
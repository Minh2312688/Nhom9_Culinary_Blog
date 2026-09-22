using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using MediatR;
namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
// Query với đầy đủ tham số phân trang, lọc, sắp xếp
// Default values đảm bảo client không cần gửi tất cả tham số
public record GetCategoriesQuery(
    int Page = 1, // Trang hiện tại
    int PageSize = 10, // Số item/trang mặc định (DefaultPageSize), giới hạn bởi MaxPageSize
    string? Search = null, // Tìm kiếm theo tên/mô tả
    string SortBy = "name", // Tên field để sắp xếp
    bool Descending = false // Chiều sắp xếp
) : IRequest<PaginatedResult<CategoryDto>>
{
    // Giá trị mặc định và giới hạn phân trang — dùng chung cho binding và validator
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
}

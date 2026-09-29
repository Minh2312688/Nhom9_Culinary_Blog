using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using MediatR;

namespace CulinaryBlog.Application.Features.Categories.Queries.GetCategories;

public record GetCategoriesQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    string SortBy = "name",
    string SortOrder = "asc") : IRequest<PaginatedResult<CategoryDto>>
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;
    public bool Descending => SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);
}

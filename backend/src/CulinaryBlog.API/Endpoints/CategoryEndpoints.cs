using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
// URL Path Versioning: /api/v1/categories, /api/v2/categories
// Khi cần breaking change: tạo /api/v2 với interface mới,
// giữ nguyên /api/v1 để không làm hỏng client cũ
namespace CulinaryBlog.API.Endpoints;
public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/categories")
            .WithTags("Categories"); // Nhóm trong Scalar UI
        // GET — lấy danh sách phân trang
        group.MapGet("/", async (
            [AsParameters] GetCategoriesQuery query, // Bind query string tự động
            ISender sender,
            CancellationToken ct) =>
            {
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            })
            .WithName("GetCategories")
            .WithSummary("Lấy danh sách danh mục có phân trang")
            .WithDescription("Hỗ trợ search, sort, và pagination. " +
            "Sử dụng ?search=... để tìm kiếm theo tên hoặc mô tả.")
            .Produces<PaginatedResult<CategoryDto>>(200);
        // POST — tạo danh mục mới
        group.MapPost("/", async (
            CreateCategoryCommand command,
            ISender sender,
            CancellationToken ct) =>
            {
                var result = await sender.Send(command, ct);
                // Trả về 201 Created với Location header trỏ đến resource mới
                return Results.Created($"/api/v1/categories/{result.Id}", result);
            })
            .WithName("CreateCategory")
            .WithSummary("Tạo danh mục mới")
            .Produces<CategoryDto>(201)
            .ProducesProblem(400) // Validation error
            .ProducesProblem(409); // Duplicate slug
        return endpoints;
    }
}


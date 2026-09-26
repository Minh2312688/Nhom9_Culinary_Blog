using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
        // GET — lấy danh sách phân trang (cache Redis 30 phút, soft-deleted bị query filter loại)
        group.MapGet("/", async (
            [AsParameters] GetCategoriesQuery query, // Bind query string tự động
            ISender sender,
            CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(query, ct);
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                return ValidationProblem(ex);
            }
        })
            .WithName("GetCategories")
            .WithSummary("Lấy danh sách danh mục có phân trang")
            .WithDescription("Hỗ trợ search, sortBy/sortOrder và pagination. " +
            "Sử dụng ?search=... để tìm kiếm theo tên hoặc mô tả.")
            .Produces<PaginatedResult<CategoryDto>>(200)
            .ProducesProblem(400); // Sai contract sortOrder/page/pageSize
        // GET — lấy một danh mục theo slug
        group.MapGet("/{slug}", async (
            string slug,
            ISender sender,
            CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(new GetCategoryBySlugQuery(slug), ct);
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                return ValidationProblem(ex);
            }
            catch (NotFoundException ex)
            {
                return NotFoundProblem(ex);
            }
        })
            .WithName("GetCategoryBySlug")
            .WithSummary("Lấy danh mục theo slug")
            .WithDescription("Trả 404 Problem Details nếu slug không tồn tại hoặc đã bị xóa mềm.")
            .Produces<CategoryDto>(200)
            .ProducesProblem(400)
            .ProducesProblem(404);
        // POST — tạo danh mục mới
        group.MapPost("/", async (
            CreateCategoryCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            try
            {
                var result = await sender.Send(command, ct);
                // 201 Created với Location trỏ tới resource mới (định tuyến theo slug)
                return Results.Created($"/api/v1/categories/{result.Slug}", result);
            }
            catch (ValidationException ex)
            {
                return ValidationProblem(ex);
            }
            catch (ConflictException ex)
            {
                return ConflictProblem(ex);
            }
        })
            .WithName("CreateCategory")
            .WithSummary("Tạo danh mục mới")
            .Produces<CategoryDto>(201)
            .ProducesProblem(400) // Validation error
            .ProducesProblem(409); // Duplicate slug/name
        // PUT — cập nhật danh mục (CONFLICT-017: Name, Description, ImageUrl, OrderIndex)
        // Slug giữ nguyên nên URL cũ không bị hỏng.
        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCategoryCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            try
            {
                // Id trong body bị bỏ qua: lấy từ route
                var result = await sender.Send(command with { Id = id }, ct);
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                return ValidationProblem(ex);
            }
            catch (NotFoundException ex)
            {
                return NotFoundProblem(ex);
            }
            catch (ConflictException ex)
            {
                return ConflictProblem(ex);
            }
        })
            .WithName("UpdateCategory")
            .WithSummary("Cập nhật danh mục")
            .WithDescription("Cập nhật Name, Description, ImageUrl, OrderIndex. Slug không đổi.")
            .Produces<CategoryDto>(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409); // Trùng tên với category khác
        // DELETE — soft delete danh mục (CONFLICT-002)
        group.MapDelete("/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            try
            {
                await sender.Send(new DeleteCategoryCommand(id), ct);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return NotFoundProblem(ex);
            }
            catch (ConflictException ex)
            {
                return ConflictProblem(ex);
            }
        })
            .WithName("DeleteCategory")
            .WithSummary("Xóa mềm danh mục")
            .WithDescription("Đặt IsDeleted = true. Trả 409 nếu category còn recipe đang hoạt động.")
            .Produces(204)
            .ProducesProblem(404)
            .ProducesProblem(409);
        return endpoints;
    }

    // Convention hiện tại của project (xem AuthEndpoints): ValidationException => 400
    // Problem Details, NotFoundException => 404, ConflictException => 409.
    private static IResult ValidationProblem(ValidationException exception) =>
        Results.ValidationProblem(
            exception.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray()),
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult NotFoundProblem(Exception exception) =>
        Results.Problem(
            title: "Not Found",
            detail: exception.Message,
            statusCode: StatusCodes.Status404NotFound);

    private static IResult ConflictProblem(Exception exception) =>
        Results.Problem(
            title: "Conflict",
            detail: exception.Message,
            statusCode: StatusCodes.Status409Conflict);
}


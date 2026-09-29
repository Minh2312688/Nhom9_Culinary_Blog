using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Features.Categories.Commands.DeleteCategory;
using CulinaryBlog.Application.Features.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Features.Categories.Queries.GetCategoryBySlug;
using CulinaryBlog.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/categories").WithTags("Categories");

        group.MapGet("/", async ([AsParameters] GetCategoriesQuery query, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .WithName("GetCategories")
            .Produces<PaginatedResult<CategoryDto>>(200)
            .ProducesProblem(400);

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetCategoryBySlugQuery(slug), ct)))
            .WithName("GetCategoryBySlug")
            .Produces<CategoryDto>(200)
            .ProducesProblem(404);

        group.MapPost("/", async (CreateCategoryCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/v1/categories/{result.Slug}", result);
        })
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin))
            .WithName("CreateCategory")
            .Produces<CategoryDto>(201)
            .ProducesProblem(400)
            .ProducesProblem(409);

        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateCategoryCommand(id, body.Name, body.Description, body.ImageUrl, body.OrderIndex), ct)))
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin))
            .WithName("UpdateCategory")
            .Produces<CategoryDto>(200)
            .ProducesProblem(400)
            .ProducesProblem(404)
            .ProducesProblem(409);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteCategoryCommand(id), ct);
            return Results.NoContent();
        })
            .RequireAuthorization(policy => policy.RequireRole(AppRoles.Admin))
            .WithName("DeleteCategory")
            .Produces(204)
            .ProducesProblem(404)
            .ProducesProblem(409);

        return endpoints;
    }

    public sealed record UpdateCategoryBody(string Name, string? Description, string? ImageUrl, int OrderIndex);
}

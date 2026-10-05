using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.API.Contracts.Recipes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipes").WithTags("Recipes");

        group.MapGet("/", async ([AsParameters] GetRecipesQuery query, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .WithName("GetRecipes").Produces<PaginatedResult<RecipeSummaryDto>>(200);

        group.MapGet("/search", async (
            [FromQuery(Name = "q")] string? q,
            [AsParameters] RecipeSearchRequest filters,
            ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRecipesQuery(
                Page: filters.Page,
                PageSize: filters.PageSize,
                CategoryId: filters.CategoryId,
                Difficulty: filters.Difficulty,
                MaxCookTime: filters.MaxCookTime,
                MinServings: filters.MinServings,
                SortBy: filters.SortBy,
                SortOrder: filters.SortOrder,
                Search: q), ct)))
            .WithName("SearchRecipes").Produces<PaginatedResult<RecipeSummaryDto>>(200).ProducesProblem(400);

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRecipeBySlugQuery(slug), ct)))
            .WithName("GetRecipeBySlug").Produces<RecipeDetailDto>(200).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/", async (CreateRecipeCommand command, ISender sender, CancellationToken ct) =>
            Results.Created($"/api/v1/recipes/{command.Title}", await sender.Send(command, ct)))
            .RequireAuthorization().WithName("CreateRecipe").Produces<RecipeDetailDto>(201).ProducesProblem(400).ProducesProblem(403);

        group.MapPut("/{id:guid}", async (Guid id, UpdateRecipeCommand command, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(command with { Id = id }, ct)))
            .RequireAuthorization().WithName("UpdateRecipe").Produces<RecipeDetailDto>(200).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        group.MapPatch("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        { await sender.Send(new PublishRecipeCommand(id), ct); return Results.NoContent(); })
            .RequireAuthorization().WithName("PublishRecipe").Produces(204).ProducesProblem(400);

        group.MapPatch("/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
        { await sender.Send(new ArchiveRecipeCommand(id), ct); return Results.NoContent(); })
            .RequireAuthorization().WithName("ArchiveRecipe").Produces(204);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        { await sender.Send(new DeleteRecipeCommand(id), ct); return Results.NoContent(); })
            .RequireAuthorization().WithName("DeleteRecipe").Produces(204).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/{id:guid}/steps", async (
            Guid id, AddRecipeStepRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddRecipeStepCommand(
                id, request.Title, request.Description, request.DurationMinutes, request.ImageUrl), ct);
            return Results.Created($"/api/v1/recipes/{id}/steps/{result.Id}", result);
        })
            .RequireAuthorization().WithName("AddRecipeStep")
            .Produces<RecipeStepDto>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        group.MapPut("/{id:guid}/steps/{stepId:guid}", async (
            Guid id, Guid stepId, ReplaceRecipeStepRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateRecipeStepCommand(
                id, stepId, request.Title, request.Description, request.DurationMinutes, request.ImageUrl), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization().WithName("UpdateRecipeStep")
            .Produces<RecipeStepDto>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapDelete("/{id:guid}/steps/{stepId:guid}", async (
            Guid id, Guid stepId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeStepCommand(id, stepId), ct);
            return Results.NoContent();
        })
            .RequireAuthorization().WithName("DeleteRecipeStep")
            .Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        return endpoints;
    }
}

public sealed record RecipeSearchRequest(
    int Page = 1,
    int PageSize = 12,
    Guid? CategoryId = null,
    string? Difficulty = null,
    int? MaxCookTime = null,
    int? MinServings = null,
    string SortBy = "createdAt",
    string SortOrder = "desc");

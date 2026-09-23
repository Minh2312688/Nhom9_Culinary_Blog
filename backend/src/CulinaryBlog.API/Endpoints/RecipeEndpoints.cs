using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.API.Contracts.Recipes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

        group.MapPost("/{id:guid}/ingredients", async (
            Guid id, AddRecipeIngredientRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddRecipeIngredientCommand(
                id, request.Name, request.Quantity, request.Unit, request.Notes, request.OrderIndex), ct);
            return Results.Created($"/api/v1/recipes/{id}/ingredients/{result.Id}", result);
        })
            .RequireAuthorization().WithName("AddRecipeIngredient")
            .Produces<CulinaryBlog.Application.DTOs.Recipes.RecipeIngredientDto>(201)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapPut("/{id:guid}/ingredients/{ingredientId:guid}", async (
            Guid id, Guid ingredientId, ReplaceRecipeIngredientRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateRecipeIngredientCommand(
                id, ingredientId, request.Name, request.Quantity, request.Unit, request.Notes, request.OrderIndex), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization().WithName("UpdateRecipeIngredient")
            .Produces<CulinaryBlog.Application.DTOs.Recipes.RecipeIngredientDto>(200)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapDelete("/{id:guid}/ingredients/{ingredientId:guid}", async (
            Guid id, Guid ingredientId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeIngredientCommand(id, ingredientId), ct);
            return Results.NoContent();
        })
            .RequireAuthorization().WithName("DeleteRecipeIngredient")
            .Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        return endpoints;
    }
}

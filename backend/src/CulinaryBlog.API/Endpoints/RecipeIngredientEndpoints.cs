using CulinaryBlog.API.Contracts.Recipes;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Commands;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeIngredientEndpoints
{
    public static IEndpointRouteBuilder MapRecipeIngredientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipes").WithTags("Recipes");

        group.MapPost("/{id:guid}/ingredients", async (
            Guid id, AddRecipeIngredientRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new AddRecipeIngredientCommand(
                id, request.Name, request.Quantity, request.Unit, request.Notes, request.OrderIndex), ct);
            return Results.Created($"/api/v1/recipes/{id}/ingredients/{result.Id}", result);
        })
            .RequireAuthorization().WithName("AddRecipeIngredient")
            .Produces<RecipeIngredientDto>(201).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapPut("/{id:guid}/ingredients/{ingId:guid}", async (
            Guid id, Guid ingId, ReplaceRecipeIngredientRequest request, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new UpdateRecipeIngredientCommand(
                id, ingId, request.Name, request.Quantity, request.Unit, request.Notes, request.OrderIndex), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization().WithName("UpdateRecipeIngredient")
            .Produces<RecipeIngredientDto>(200).ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapDelete("/{id:guid}/ingredients/{ingId:guid}", async (
            Guid id, Guid ingId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeIngredientCommand(id, ingId), ct);
            return Results.NoContent();
        })
            .RequireAuthorization().WithName("DeleteRecipeIngredient")
            .Produces(204).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        return endpoints;
    }
}

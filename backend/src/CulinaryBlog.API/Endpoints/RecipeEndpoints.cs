using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Application.Features.Recipes.Queries;
using CulinaryBlog.Domain.Constants;
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

        endpoints.MapGet("/api/v1/admin/recipes", async (
            int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetRecipesQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 12,
                    IncludeUnpublished: true), ct)))
            .RequireAuthorization(AppPolicies.AdminPolicy)
            .WithName("GetAdminRecipes")
            .Produces<PaginatedResult<RecipeSummaryDto>>(200)
            .ProducesProblem(401)
            .ProducesProblem(403);

        group.MapGet("/", async ([AsParameters] GetRecipesQuery query, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(query, ct)))
            .WithName("GetRecipes").Produces<PaginatedResult<RecipeSummaryDto>>(200);

        group.MapGet("/mine", async (int? page, int? pageSize, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRecipesQuery(
                Page: page ?? 1, PageSize: pageSize ?? 12, OnlyMine: true), ct)))
            .RequireAuthorization().WithName("GetMyRecipes").Produces<PaginatedResult<RecipeSummaryDto>>(200)
            .ProducesProblem(401);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetPrivateRecipeByIdQuery(id), ct)))
            .RequireAuthorization().WithName("GetPrivateRecipeById")
            .Produces<RecipeDetailDto>(200).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);

        group.MapGet("/{slug}", async (string slug, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetRecipeBySlugQuery(slug), ct)))
            .WithName("GetRecipeBySlug").Produces<RecipeDetailDto>(200).ProducesProblem(403).ProducesProblem(404);

        group.MapPost("/", async (CreateRecipeCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/v1/recipes/{Uri.EscapeDataString(result.Slug)}", result);
        })
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

        endpoints.MapRecipeSearchEndpoints();
        endpoints.MapRecipeStepEndpoints();
        endpoints.MapRecipeIngredientEndpoints();

        return endpoints;
    }
}

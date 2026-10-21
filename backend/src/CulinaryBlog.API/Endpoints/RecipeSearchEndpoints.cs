using CulinaryBlog.API.Contracts.Recipes;
using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeSearchEndpoints
{
    public static IEndpointRouteBuilder MapRecipeSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipes").WithTags("Recipes");

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

        return endpoints;
    }
}

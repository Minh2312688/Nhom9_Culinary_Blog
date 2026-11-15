using CulinaryBlog.API.Contracts.Recipes;
using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Commands;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeStepEndpoints
{
    public static IEndpointRouteBuilder MapRecipeStepEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipes").WithTags("Recipes");

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

        group.MapPost("/{id:guid}/steps/{stepId:guid}/image", async (
            Guid id,
            Guid stepId,
            HttpRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["A multipart/form-data request is required."]
                });
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["file"] = ["The file field is required."]
                });
            }

            using var content = file.OpenReadStream();
            var result = await sender.Send(new UploadRecipeStepImageCommand(
                id, stepId, content, file.FileName, file.ContentType, file.Length), ct);
            return Results.Ok(result);
        })
            .RequireAuthorization().WithName("UploadRecipeStepImage")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<RecipeStepDto>(200).ProducesValidationProblem(400)
            .ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503);

        return endpoints;
    }
}

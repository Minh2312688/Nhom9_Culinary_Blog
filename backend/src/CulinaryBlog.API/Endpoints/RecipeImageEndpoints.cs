using CulinaryBlog.Application.DTOs.Recipes;
using CulinaryBlog.Application.Features.Recipes.Commands;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeImageEndpoints
{
    public static IEndpointRouteBuilder MapRecipeImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/recipes/{id:guid}/images")
            .WithTags("Recipe Images")
            .RequireAuthorization();

        group.MapPost("/", async (Guid id, HttpRequest request, ISender sender, CancellationToken ct) =>
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

            var primaryValue = form["isPrimary"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(primaryValue) && !bool.TryParse(primaryValue, out _))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["isPrimary"] = ["The isPrimary field must be true or false."]
                });
            }

            var isPrimary = bool.TryParse(primaryValue, out var parsedPrimary) && parsedPrimary;
            using var content = file.OpenReadStream();
            var result = await sender.Send(new UploadRecipeImageCommand(
                id, content, file.FileName, file.ContentType, file.Length,
                form["altText"].FirstOrDefault(), isPrimary), ct);

            return Results.Created($"/api/v1/recipes/{id}/images/{result.ImageId}", result);
        })
            .WithName("UploadRecipeImage")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<RecipeImageUploadDto>(201)
            .ProducesValidationProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(503);

        group.MapPatch("/{imageId:guid}/primary", async (
            Guid id, Guid imageId, ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new SetPrimaryRecipeImageCommand(id, imageId), ct)))
            .WithName("SetPrimaryRecipeImage")
            .Produces<RecipeImageDto>(200)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404);

        group.MapDelete("/{imageId:guid}", async (
            Guid id, Guid imageId, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteRecipeImageCommand(id, imageId), ct);
            return Results.NoContent();
        })
            .WithName("DeleteRecipeImage")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(401)
            .ProducesProblem(403)
            .ProducesProblem(404)
            .ProducesProblem(503);

        return endpoints;
    }
}
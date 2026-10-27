using CulinaryBlog.Application.DTOs.Authors;
using CulinaryBlog.Application.Features.Authors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class AuthorEndpoints
{
    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/authors")
            .WithTags("Authors")
            .MapGet("/{id}", async (
                string id,
                ISender sender,
                CancellationToken cancellationToken,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 12) =>
                Results.Ok(await sender.Send(
                    new GetAuthorProfileQuery(id, page, pageSize),
                    cancellationToken)))
            .AllowAnonymous()
            .WithName("GetAuthorProfile")
            .Produces<AuthorProfilePageDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}

using CulinaryBlog.Application.System.Queries.GetBaseStatus;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/system/base-status", async (
            ISender sender,
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new GetBaseStatusQuery(environment.EnvironmentName),
                cancellationToken);

            return Results.Ok(result);
        });

        return endpoints;
    }
}

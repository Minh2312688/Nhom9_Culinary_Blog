using CulinaryBlog.Application.Features.System.Queries.GetBaseStatus;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using CulinaryBlog.API.Observability;

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

        endpoints.MapHealthChecks(
            "/health/ready",
            HealthChecksSetup.CreateOptions(HealthChecksSetup.ReadyTag, json: false))
            .AllowAnonymous()
            .DisableRateLimiting();

        endpoints.MapHealthChecks(
            "/health",
            HealthChecksSetup.CreateOptions(HealthChecksSetup.AggregateTag, json: true))
            .AllowAnonymous()
            .DisableRateLimiting();

        // FR-OBS-001 (liveness slice): reports only that the API process is
        // alive. No dependency checks (PostgreSQL/Redis/MinIO), no internal
        // details, accessible anonymously, and the response is never cached.
        endpoints.MapGet("/health/live", (HttpContext context) =>
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache";
            context.Response.Headers["Pragma"] = "no-cache";

            return Results.Text("Healthy", "text/plain");
        })
        .DisableRateLimiting();

        return endpoints;
    }
}

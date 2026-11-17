using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Application.Features.Auth.Admin;
using CulinaryBlog.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/users", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new GetAdminUsersQuery(), ct)))
            .RequireAuthorization(AppPolicies.AdminPolicy)
            .WithName("GetAdminUsers")
            .WithTags("Admin")
            .Produces<IReadOnlyList<AdminUserDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }
}

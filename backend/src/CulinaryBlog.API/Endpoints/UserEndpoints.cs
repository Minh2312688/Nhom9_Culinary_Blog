using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Constants;
using CulinaryBlog.Application.Features.Users.GetUsers;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var usersGroup = endpoints.MapGroup("/api/v1/users")
            .RequireAuthorization(CulinaryBlog.Domain.Constants.AppPolicies.AdminPolicy);

        // FR-ADMIN-USERS: List all users with pagination, role filter, and keyword search
        usersGroup.MapGet("/", async (
            int? page,
            int? pageSize,
            string? role,
            string? searchTerm,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var query = new GetUsersQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 10,
                    Role: role,
                    SearchTerm: searchTerm);

                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                return Results.ValidationProblem(
                    ex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        // Also map /api/v1/auth/users to support both URL conventions smoothly
        var authUsersGroup = endpoints.MapGroup("/api/v1/auth/users")
            .RequireAuthorization(CulinaryBlog.Domain.Constants.AppPolicies.AdminPolicy);

        authUsersGroup.MapGet("/", async (
            int? page,
            int? pageSize,
            string? role,
            string? searchTerm,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var query = new GetUsersQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 10,
                    Role: role,
                    SearchTerm: searchTerm);

                var result = await sender.Send(query, cancellationToken);
                return Results.Ok(result);
            }
            catch (ValidationException ex)
            {
                return Results.ValidationProblem(
                    ex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                    statusCode: StatusCodes.Status400BadRequest);
            }
        });

        return endpoints;
    }
}

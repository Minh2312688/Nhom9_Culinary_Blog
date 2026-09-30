using System.Security.Claims;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Logout;
using CulinaryBlog.Application.Features.Auth.Profile;
using CulinaryBlog.Application.Features.Auth.TokenRefresh;
using CulinaryBlog.Application.Features.Auth.Register;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CulinaryBlog.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var authGroup = endpoints.MapGroup("/api/v1/auth")
            .RequireRateLimiting("AuthRateLimitPolicy");

        // FR-AUTH-001: Register
        authGroup.MapPost("/register", async (
            RegisterRequestDto request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new RegisterCommand(request.Email, request.Password, request.DisplayName);
            var result = await sender.Send(command, cancellationToken);
            return Results.Created($"/api/v1/users/{result.UserId}", result);
        });

        // FR-AUTH-002: Login
        authGroup.MapPost("/login", async (
            LoginRequestDto request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new LoginCommand(request.Email, request.Password);
            var result = await sender.Send(command, cancellationToken);
            return Results.Ok(result);
        });

        // FR-AUTH-003: Google Login
        authGroup.MapPost("/google", async (
            GoogleLoginRequestDto request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new GoogleLoginCommand(request.IdToken);
            var result = await sender.Send(command, cancellationToken);
            return Results.Ok(result);
        });

        // FR-AUTH-004: Refresh Token Rotation
        authGroup.MapPost("/refresh", async (
            RefreshTokenRequestDto request,
            HttpContext httpContext,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new RefreshTokenCommand(
                request.RefreshToken,
                httpContext.Connection.RemoteIpAddress?.ToString());
            var result = await sender.Send(command, cancellationToken);
            return Results.Ok(result);
        });

        // FR-AUTH-005: Logout (Idempotent)
        authGroup.MapPost("/logout", async (
            LogoutRequestDto request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedException("User is not authenticated.");
            }

            var command = new LogoutCommand(request.RefreshToken, userId);
            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization();

        // FR-AUTH-006: View Current Profile
        authGroup.MapGet("/me", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedException("User is not authenticated.");
            }

            var query = new GetCurrentUserQuery(userId);
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        }).RequireAuthorization();

        return endpoints;
    }
}

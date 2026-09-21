using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.DTOs.Auth;
using CulinaryBlog.Application.Features.Auth.GoogleLogin;
using CulinaryBlog.Application.Features.Auth.Login;
using CulinaryBlog.Application.Features.Auth.Register;
using FluentValidation;
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
            try
            {
                var command = new RegisterCommand(request.Email, request.Password, request.DisplayName);
                var result = await sender.Send(command, cancellationToken);
                return Results.Created($"/api/v1/users/{result.UserId}", result);
            }
            catch (ValidationException ex)
            {
                return Results.ValidationProblem(
                    ex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()),
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (ConflictException ex)
            {
                return Results.Problem(
                    title: "Conflict",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        });

        // FR-AUTH-002: Login
        authGroup.MapPost("/login", async (
            LoginRequestDto request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var command = new LoginCommand(request.Email, request.Password);
                var result = await sender.Send(command, cancellationToken);
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
            catch (UnauthorizedException ex)
            {
                return Results.Problem(
                    title: "Unauthorized",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (AccountLockedException ex)
            {
                return Results.Problem(
                    title: "Locked",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status423Locked);
            }
        });

        // FR-AUTH-003: Google Login
        authGroup.MapPost("/google", async (
            GoogleLoginRequestDto request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var command = new GoogleLoginCommand(request.IdToken);
                var result = await sender.Send(command, cancellationToken);
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
            catch (InvalidGoogleTokenException ex)
            {
                return Results.Problem(
                    title: "Bad Request",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
            catch (UnauthorizedException ex)
            {
                return Results.Problem(
                    title: "Unauthorized",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status401Unauthorized);
            }
        });

        return endpoints;
    }
}

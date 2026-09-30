using System.Diagnostics;
using System.Text.Json;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.API.Middleware;

/// <summary>
/// Catches unhandled exceptions globally and formats RFC 7807 Problem Details responses.
/// Protects against leaking sensitive internal details, connection strings, or stack traces.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during request execution: {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var instance = context.Request.Path.Value ?? "/";

        var (statusCode, title, type, detail, errors) = MapException(exception);

        context.Response.StatusCode = statusCode;

        object problemDetails;

        if (errors != null)
        {
            problemDetails = new HttpValidationProblemDetails(errors)
            {
                Type = type,
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = instance,
                Extensions = { ["traceId"] = traceId }
            };
        }
        else
        {
            problemDetails = new ProblemDetails
            {
                Type = type,
                Title = title,
                Status = statusCode,
                Detail = detail,
                Instance = instance,
                Extensions = { ["traceId"] = traceId }
            };
        }

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, problemDetails.GetType(), jsonOptions));
    }

    private static (int StatusCode, string Title, string Type, string Detail, IDictionary<string, string[]>? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                "The request failed validation checks.",
                valEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Not Found",
                "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                notFoundEx.Message,
                null
            ),

            UnauthorizedException unauthEx => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                unauthEx.Message,
                null
            ),

            ConflictException conflictEx => (
                StatusCodes.Status409Conflict,
                "Conflict",
                "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                conflictEx.Message,
                null
            ),

            AccountLockedException lockedEx => (
                StatusCodes.Status423Locked,
                "Locked",
                "https://tools.ietf.org/html/rfc4918#section-11.3",
                lockedEx.Message,
                null
            ),

            InvalidGoogleTokenException googleEx => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                googleEx.Message,
                null
            ),

            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                "Domain Rule Violation",
                "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                domainEx.Message,
                null
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                "An unexpected error occurred while processing your request.",
                null
            )
        };
    }
}

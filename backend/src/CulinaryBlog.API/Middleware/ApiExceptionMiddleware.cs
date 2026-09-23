using CulinaryBlog.Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.API.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception, "Unhandled exception after response started.");
                throw;
            }

            var result = CreateProblem(exception, context);
            if (result is null)
            {
                logger.LogError(exception, "Unhandled request exception.");
                result = Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "An unexpected error occurred.",
                    instance: context.Request.Path);
            }
            else if (exception is not ValidationException)
            {
                logger.LogWarning(exception, "Request failed and was mapped to a client error response.");
            }

            await result.ExecuteAsync(context);
        }
    }

    private static IResult? CreateProblem(Exception exception, HttpContext context) => exception switch
    {
        ValidationException validation => Results.ValidationProblem(
            validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()),
            statusCode: StatusCodes.Status400BadRequest,
            instance: context.Request.Path),
        ForbiddenAccessException => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: exception.Message, instance: context.Request.Path),
        NotFoundException => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: exception.Message, instance: context.Request.Path),
        UnauthorizedException => Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: exception.Message, instance: context.Request.Path),
        ConcurrencyException or ConflictException => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: exception.Message, instance: context.Request.Path),
        InvalidOperationException => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: exception.Message, instance: context.Request.Path),
        _ => null
    };
}

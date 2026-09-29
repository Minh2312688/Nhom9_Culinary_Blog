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

            var result = CreateProblem(exception, context.Request.Path);
            if (result is null)
            {
                logger.LogError(exception, "Unhandled request exception.");
                result = Results.Problem(statusCode: 500, title: "An unexpected error occurred.", instance: context.Request.Path);
            }
            else if (exception is not ValidationException)
            {
                logger.LogWarning(exception, "Request failed and was mapped to a client error response.");
            }

            await result.ExecuteAsync(context);
        }
    }

    private static IResult? CreateProblem(Exception exception, PathString path) => exception switch
    {
        ValidationException validation => Results.ValidationProblem(
            validation.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).Distinct().ToArray()),
            statusCode: 400,
            instance: path),
        ForbiddenAccessException => Results.Problem(statusCode: 403, title: "Forbidden", detail: exception.Message, instance: path),
        NotFoundException => Results.Problem(statusCode: 404, title: "Not Found", detail: exception.Message, instance: path),
        UnauthorizedException => Results.Problem(statusCode: 401, title: "Unauthorized", detail: exception.Message, instance: path),
        ConcurrencyException or ConflictException => Results.Problem(statusCode: 409, title: "Conflict", detail: exception.Message, instance: path),
        InvalidOperationException => Results.Problem(statusCode: 400, title: "Bad Request", detail: exception.Message, instance: path),
        _ => null
    };
}

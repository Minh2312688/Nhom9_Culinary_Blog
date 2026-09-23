using System.Text.Json;
using CulinaryBlog.API.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Middleware;

public sealed class ApiExceptionMiddlewareTests
{
    [Fact]
    public async Task ValidationException_Returns400ProblemDetailsWithFieldErrors()
    {
        var middleware = new ApiExceptionMiddleware(_ => throw new ValidationException(
        [new FluentValidation.Results.ValidationFailure("Name", "Name is required.")] ), NullLogger<ApiExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ValidationProblemDetails>(context.Response.Body);
        Assert.NotNull(body);
        Assert.Contains("Name", body.Errors.Keys);
    }

    [Fact]
    public async Task ForbiddenAccessException_Returns403ProblemDetails()
    {
        var middleware = new ApiExceptionMiddleware(_ => throw new CulinaryBlog.Application.Common.Exceptions.ForbiddenAccessException(),
            NullLogger<ApiExceptionMiddleware>.Instance);
        var context = NewContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        return context;
    }
}

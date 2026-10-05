using System.Text.Json;
using CulinaryBlog.API.Middleware;
using CulinaryBlog.Application.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Middleware;

public sealed class ApiExceptionMiddlewareTests
{
    [Fact]
    public async Task StorageUnavailable_Returns503WithoutLeakingExceptionDetails()
    {
        var middleware = new ApiExceptionMiddleware(
            _ => Task.FromException(new StorageUnavailableException("secret endpoint", new Exception("secret key"))),
            NullLogger<ApiExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/recipes/recipe-id/images";
        context.RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var response = await JsonDocument.ParseAsync(context.Response.Body);
        var body = response.RootElement.GetRawText();
        Assert.Contains("temporarily unavailable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
    }
}

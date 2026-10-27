using CulinaryBlog.API.Observability;
using CulinaryBlog.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

/// <summary>
/// ST-10: covers S-12's request-scoped log context. A Serilog logger enriched
/// with FromLogContext and a CollectingLogSink captures the event written from
/// inside the middleware's scope, so the pushed properties are observable.
/// </summary>
public sealed class RequestLogContextMiddlewareTests
{
    private const string ProbeMessage = "request-log-context-probe";
    private const string RequestPath = "/x";

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedUser_PushesRequestPathHttpMethodAndUserId()
    {
        var sink = new CollectingLogSink();
        using var logger = CreateLogger(sink);
        using var provider = BuildRequestServices("user-1");
        var middleware = CreateMiddleware(logger);

        var context = CreateContext(provider);

        await middleware.InvokeAsync(context);

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(RequestPath, GetScalarValue(logEvent, "RequestPath"));
        Assert.Equal("GET", GetScalarValue(logEvent, "HttpMethod"));
        Assert.Equal("user-1", GetScalarValue(logEvent, "UserId"));
    }

    [Fact]
    public async Task InvokeAsync_WithoutUser_OmitsUserId()
    {
        var sink = new CollectingLogSink();
        using var logger = CreateLogger(sink);
        using var provider = BuildRequestServices(null);
        var middleware = CreateMiddleware(logger);

        var context = CreateContext(provider);

        await middleware.InvokeAsync(context);

        var logEvent = Assert.Single(sink.Events);
        Assert.Equal(RequestPath, GetScalarValue(logEvent, "RequestPath"));
        Assert.Equal("GET", GetScalarValue(logEvent, "HttpMethod"));
        Assert.False(logEvent.Properties.ContainsKey("UserId"));
    }

    private static RequestLogContextMiddleware CreateMiddleware(Serilog.ILogger logger)
        => new(_ =>
        {
            logger.Information(ProbeMessage);
            return Task.CompletedTask;
        });

    private static DefaultHttpContext CreateContext(IServiceProvider requestServices)
    {
        var context = new DefaultHttpContext { RequestServices = requestServices };
        context.Request.Path = RequestPath;
        context.Request.Method = HttpMethods.Get;
        return context;
    }

    private static Serilog.Core.Logger CreateLogger(CollectingLogSink sink)
        => new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

    private static ServiceProvider BuildRequestServices(string? userId)
        => new ServiceCollection()
            .AddSingleton<ICurrentUserService>(new FakeCurrentUserService(userId))
            .BuildServiceProvider();

    private static string? GetScalarValue(LogEvent logEvent, string propertyName)
        => logEvent.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;

    private sealed class FakeCurrentUserService(string? userId) : ICurrentUserService
    {
        public string? UserId => userId;
        public bool IsAdmin => false;
        public bool IsAuthenticated => !string.IsNullOrEmpty(userId);
    }
}

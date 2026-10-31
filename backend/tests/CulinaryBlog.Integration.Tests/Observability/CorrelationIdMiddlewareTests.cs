using CulinaryBlog.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System.Net;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Observability;

[Collection("SerilogSinks")]
public sealed class CorrelationIdMiddlewareTests
{
    private const string HeaderName = "X-Correlation-ID";

    [Fact]
    public async Task InvokeAsync_ValidHeader_KeepsCorrelationId()
    {
        var sink = new CollectingLogSink();
        using var logger = CreateLogger(sink);
        var middleware = CreateMiddleware(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers[HeaderName] = "abc-123";

        await middleware.InvokeAsync(context);

        Assert.Equal("abc-123", context.Response.Headers[HeaderName].ToString());
        Assert.Equal("abc-123", context.Items["CorrelationId"]);
    }

    [Fact]
    public async Task InvokeAsync_NoHeader_GeneratesCorrelationId()
    {
        var sink = new CollectingLogSink();
        using var logger = CreateLogger(sink);
        var middleware = CreateMiddleware(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        context.Request.Method = HttpMethods.Get;

        await middleware.InvokeAsync(context);

        var id = context.Response.Headers[HeaderName].ToString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Equal(32, id.Length);
    }

    [Fact]
    public async Task InvokeAsync_InvalidHeader_ReplacesAndSucceeds()
    {
        var sink = new CollectingLogSink();
        using var logger = CreateLogger(sink);
        var middleware = CreateMiddleware(logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers[HeaderName] = "bad value!";

        await middleware.InvokeAsync(context);

        var id = context.Response.Headers[HeaderName].ToString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.NotEqual("bad value!", id);
    }

    [Fact]
    public async Task GetRoot_LogHasCorrelationId()
    {
        using var factory = new SerilogWebApplicationFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(HeaderName));

        var sink = factory.Sink;
        LogEvent? evt = null;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            evt = sink.Events.SingleOrDefault(logEvent =>
                GetScalarValue(logEvent, "RequestPath") == "/" &&
                logEvent.Properties.ContainsKey("RequestMethod") &&
                logEvent.Properties.ContainsKey("StatusCode") &&
                logEvent.Properties.ContainsKey("Elapsed"));
            if (evt is not null)
            {
                break;
            }
            await Task.Delay(50);
        }
        Assert.NotNull(evt);
        Assert.True(evt!.Properties.ContainsKey("CorrelationId"));
        var actualId = (evt.Properties["CorrelationId"] as ScalarValue)?.Value?.ToString();
        var expectedId = response.Headers.GetValues(HeaderName).FirstOrDefault();
        Assert.Equal(expectedId, actualId);
    }

    private static string? GetScalarValue(LogEvent logEvent, string propertyName)
        => logEvent.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue scalar
            ? scalar.Value?.ToString()
            : null;
    private static CorrelationIdMiddleware CreateMiddleware(Serilog.ILogger logger)
        => new(_ =>
        {
            logger.Information("probe");
            return Task.CompletedTask;
        });

    private static Serilog.Core.Logger CreateLogger(CollectingLogSink sink)
        => new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();
}
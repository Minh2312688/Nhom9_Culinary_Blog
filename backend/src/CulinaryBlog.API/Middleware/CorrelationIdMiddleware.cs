using System.Diagnostics;
using Serilog.Context;

namespace CulinaryBlog.API.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers.TryGetValue("X-Correlation-ID", out var raw)
            ? raw.ToString()
            : null);

        context.Items["CorrelationId"] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        if (!context.Response.Headers.ContainsKey("X-Correlation-ID"))
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
        }

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && raw!.Length <= MaxLength
            && raw.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
        {
            return raw;
        }

        return Guid.NewGuid().ToString("N");
    }
}
using CulinaryBlog.Application.Contracts;
using Serilog.Context;

namespace CulinaryBlog.API.Observability;

/// <summary>
/// FR-OBS-002 / CONS-010 log context. Pushes <c>RequestPath</c>, <c>HttpMethod</c>
/// and, when authenticated, <c>UserId</c> into the Serilog <see cref="LogContext"/>
/// for the remainder of the request. It runs after authentication so the
/// current-user service can resolve a principal, and it does not create a
/// CorrelationId: that property is owned by TV1's CorrelationId middleware
/// (S-08), which is not part of TV4.
///
/// Known gaps (reported, not fixed here): the request summary event emitted by
/// <c>UseSerilogRequestLogging</c> and logs written by TV1's
/// <c>ApiExceptionMiddleware</c> execute outside this scope, so they do not carry
/// these properties. The middleware intentionally does not log anything itself.
/// </summary>
public sealed class RequestLogContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? string.Empty))
        using (LogContext.PushProperty("HttpMethod", context.Request.Method))
        {
            var userId = context.RequestServices.GetRequiredService<ICurrentUserService>().UserId;
            if (string.IsNullOrEmpty(userId))
            {
                await next(context);
                return;
            }

            using (LogContext.PushProperty("UserId", userId))
            {
                await next(context);
            }
        }
    }
}

using MediatR;
using Microsoft.Extensions.Logging;
using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Application.Contracts;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Audits MediatR commands without logging request or exception payloads.
/// </summary>
public sealed class AuditLoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;

    public AuditLoggingBehavior(
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal))
        {
            return await next(cancellationToken);
        }

        var audit = request as IAuditCommand;
        var auditAction = audit?.AuditAction ?? typeof(TRequest).Name;
        var entityType = audit?.EntityType;
        var entityId = audit?.EntityId;
        var userId = _currentUserService.UserId;
        TResponse response;

        try
        {
            response = await next(cancellationToken);
        }
        catch (Exception exception)
        {
            var outcome = exception is OperationCanceledException ? "Canceled" : "Failure";
            var logLevel = exception is OperationCanceledException ? LogLevel.Information : LogLevel.Warning;
            try
            {
                _logger.Log(
                    logLevel,
                    "Audit {AuditAction} {EntityType} {EntityId} by {UserId} at {Timestamp} ({Outcome}, {ExceptionType})",
                    auditAction,
                    entityType,
                    entityId,
                    userId,
                    DateTimeOffset.UtcNow,
                    outcome,
                    exception.GetType().Name);
            }
            catch (Exception auditException)
            {
                var auditExceptionType = auditException.GetType().Name;
                exception.Data["AuditLoggingFailureType"] = auditExceptionType;
                System.Diagnostics.Trace.TraceError(
                    "Audit logging failed for {0}; logger exception type {1}.",
                    auditAction,
                    auditExceptionType);
            }

            throw;
        }

        entityId ??= audit is null ? null : ResolveEntityId(audit, response);

        _logger.LogInformation(
            "Audit {AuditAction} {EntityType} {EntityId} by {UserId} at {Timestamp} ({Outcome})",
            auditAction,
            entityType,
            entityId,
            userId,
            DateTimeOffset.UtcNow,
            "Success");

        return response;
    }

    private static Guid? ResolveEntityId(IAuditCommand audit, TResponse response)
    {
        if (audit.EntityId.HasValue)
        {
            return audit.EntityId;
        }

        if (response is null)
        {
            return null;
        }

        try
        {
            var idProperty = typeof(TResponse).GetProperty("Id");
            if (idProperty?.PropertyType == typeof(Guid))
            {
                var value = idProperty.GetValue(response);
                if (value is Guid guid && guid != Guid.Empty)
                {
                    return guid;
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
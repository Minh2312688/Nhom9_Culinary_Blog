using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Services;

public sealed class AuditTrail(
    ILogger<AuditTrail> logger,
    ICurrentUserService currentUserService) : IAuditTrail
{
    public void LogSuccess(string action, string entityType, Guid? entityId)
        => Write(
            LogLevel.Information,
            action,
            entityType,
            entityId,
            "Success",
            exceptionType: null);

    public void LogFailure(string action, string entityType, Guid? entityId, Exception exception)
        => Write(
            LogLevel.Warning,
            action,
            entityType,
            entityId,
            "Failure",
            exception.GetType().Name);

    public void LogCanceled(
        string action,
        string entityType,
        Guid? entityId,
        OperationCanceledException exception)
        => Write(
            LogLevel.Information,
            action,
            entityType,
            entityId,
            "Canceled",
            exception.GetType().Name);

    private void Write(
        LogLevel level,
        string action,
        string entityType,
        Guid? entityId,
        string outcome,
        string? exceptionType)
    {
        try
        {
            logger.Log(
                level,
                "Audit {AuditAction} {EntityType} {EntityId} by {UserId} at {Timestamp} ({Outcome}{ExceptionSuffix})",
                action,
                entityType,
                entityId,
                currentUserService.UserId ?? "Anonymous",
                DateTimeOffset.UtcNow,
                outcome,
                exceptionType is null ? string.Empty : $", {exceptionType}");
        }
        catch (Exception auditException)
        {
            System.Diagnostics.Trace.TraceError(
                "Audit logging failed for {0}; logger exception type {1}.",
                action,
                auditException.GetType().Name);
        }
    }
}

namespace CulinaryBlog.Application.Common.Contracts;

public interface IAuditTrail
{
    void LogSuccess(string action, string entityType, Guid? entityId);
    void LogFailure(string action, string entityType, Guid? entityId, Exception exception);
    void LogCanceled(string action, string entityType, Guid? entityId, OperationCanceledException exception);
}

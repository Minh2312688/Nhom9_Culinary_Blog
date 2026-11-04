using MediatR;
using Microsoft.Extensions.Logging;
using CulinaryBlog.Application.Common.Contracts;
using CulinaryBlog.Application.Contracts;

namespace CulinaryBlog.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior: chỉ audit các request implement IAuditCommand.
/// Ghi audit SAU khi handler thành công. Handler nthrows lỗi thì KHÔNG audit và KHÔNG catch nuốt lỗi.
/// UserId lấy từ ICurrentUserService. CorrelationId tự động có từ Serilog LogContext (O-02).
/// Application KHÔNG dùngASP.NET HttpContext.
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
        // Chỉ audit các request implement IAuditCommand
        if (request is not IAuditCommand audit)
        {
            return await next(cancellationToken);
        }

        try
        {
            var response = await next(cancellationToken);

            // Chỉ ghi audit sau khi handler THÀNH CÔNG
            var entityId = ResolveEntityId(audit, response);

            _logger.LogInformation(
                "Audit {AuditAction} {EntityType} {EntityId} by {UserId} at {Timestamp} ({Outcome})",
                audit.AuditAction,
                audit.EntityType,
                entityId,
                _currentUserService.UserId,
                DateTimeOffset.UtcNow,
                "Success");

            return response;
        }
        catch
        {
            // Handler nthrows lỗi: KHÔNG audit, KHÔNG catch nuốt — để exception propagates
            throw;
        }
    }

    /// <summary>
    /// Lấy EntityId: ưu tiên từ IAuditCommand (Publish/Delete đã có Id),
    /// nếu null thì lấy từ response bằng reflection an toàn (Create: response.Id).
    /// </summary>
    private static Guid? ResolveEntityId(IAuditCommand audit, TResponse response)
    {
        // Ưu tiên giá trị từ command (Publish/Delete đã set Id)
        if (audit.EntityId.HasValue)
        {
            return audit.EntityId;
        }

        // Create: response là RecipeDetailDto có property Id (Guid)
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
            // Reflection an toàn: không có thì trả null
        }

        return null;
    }
}
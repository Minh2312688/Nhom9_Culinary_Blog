namespace CulinaryBlog.Application.Common.Contracts;

/// <summary>
/// Optional metadata for audit commands. All requests named *Command are audited;
/// commands implement this interface when they can provide entity details.
/// </summary>
public interface IAuditCommand
{
    string AuditAction { get; }
    string EntityType { get; }
    Guid? EntityId { get; }
}
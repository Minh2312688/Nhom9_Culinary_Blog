namespace CulinaryBlog.Application.Common.Contracts;

/// <summary>
/// Marker interface cho các MediatR command thực hiện thao tác ghi dữ liệu
/// (create/update/delete/change status). Chỉ command implement interface này mới được audit.
/// Dùng explicit interface implementation để không cần đổi tham số record.
/// </summary>
public interface IAuditCommand
{
    string AuditAction { get; }  // ví dụ "Recipe.Create"
    string EntityType { get; }   // ví dụ "Recipe"
    Guid? EntityId { get; }      // null nếu chưa biết
}
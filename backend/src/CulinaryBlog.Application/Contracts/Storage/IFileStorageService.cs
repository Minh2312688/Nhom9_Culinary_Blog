namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Contract upload/xóa file dùng chung cho category, recipe image và các module cần lưu file.
/// Task hiện tại (MinIO foundation) chỉ chốt contract: chưa có implementation MinIO,
/// nên FR-FILE-001/FR-FILE-002 chưa hoàn thành toàn bộ.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Upload file và trả thông tin object đã lưu (không phải response contract của API upload).</summary>
    Task<FileUploadResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xóa file theo URL/object name; idempotent khi object không tồn tại (FR-FILE-002).</summary>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
}

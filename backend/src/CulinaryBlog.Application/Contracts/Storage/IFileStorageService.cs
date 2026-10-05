namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Contract upload/xóa file dùng chung cho category, recipe image và các module cần lưu file.
/// Infrastructure implements this contract with MinIO; Application handlers depend only on this interface.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Upload file và trả thông tin object đã lưu (không phải response contract của API upload).</summary>
    Task<FileUploadResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xóa file theo URL/object name; idempotent khi object không tồn tại (FR-FILE-002).</summary>
    Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default);
}

namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Validate file ảnh trước khi lưu trữ (CONS-007, NFR-SEC-004).
/// </summary>
public interface IFileValidationService
{
    /// <summary>Kiểm tra size, magic bytes và tính nhất quán với extension/MIME client khai báo.</summary>
    /// <exception cref="CulinaryBlog.Application.Common.Exceptions.InvalidFileException">File không hợp lệ.</exception>
    FileValidationResult Validate(FileUploadRequest request);
}

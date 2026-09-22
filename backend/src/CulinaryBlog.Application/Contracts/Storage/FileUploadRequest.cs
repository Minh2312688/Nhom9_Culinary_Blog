namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Dữ liệu upload do caller cung cấp (TV2 Recipe image handler sẽ tạo từ IFormFile).
/// Cố ý dùng Stream thay vì IFormFile để Application layer không phụ thuộc ASP.NET Core.
/// </summary>
/// <param name="Content">Nội dung file; phải seekable để đọc magic bytes mà không tiêu thụ stream.</param>
/// <param name="FileName">Tên file gốc client gửi; chỉ dùng để kiểm tra extension.</param>
/// <param name="ContentType">MIME type client khai báo; có thể null.</param>
/// <param name="Length">Độ dài file client khai báo (byte).</param>
/// <param name="Folder">Folder đích trong bucket, xem <see cref="StorageFolders"/>.</param>
public sealed record FileUploadRequest(
    Stream Content,
    string FileName,
    string? ContentType,
    long Length,
    string Folder);

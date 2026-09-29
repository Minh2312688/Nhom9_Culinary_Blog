namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Kết quả ở mức storage của một lần upload.
/// Đây KHÔNG phải response contract của API upload ảnh: shape đang là CONFLICT-024 (OPEN),
/// không do TV4 tự quyết định.
/// </summary>
public sealed record FileUploadResult(
    string ObjectName,
    string Url,
    string ContentType,
    long SizeBytes);

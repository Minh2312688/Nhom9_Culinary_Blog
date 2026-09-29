namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Kết quả validate file ảnh. Format là nguồn tin cậy duy nhất (magic bytes);
/// ContentType và Extension là giá trị canonical dùng khi lưu trữ.
/// </summary>
public sealed record FileValidationResult(
    ImageFileFormat Format,
    string ContentType,
    string Extension,
    long SizeBytes);

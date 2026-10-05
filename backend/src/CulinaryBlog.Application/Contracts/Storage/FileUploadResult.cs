namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Kết quả ở mức storage của một lần upload.
/// API handlers map this storage result to their public upload response contract.
/// </summary>
public sealed record FileUploadResult(
    string ObjectName,
    string Url,
    string ContentType,
    long SizeBytes);

namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Mã lỗi file không hợp lệ, dùng để tầng API map sang ProblemDetails.
/// Việc map sang HTTP status chưa thuộc phạm vi task này (project chưa có Global Exception Handler).
/// </summary>
public static class FileErrorCodes
{
    public const string FileEmpty = "FILE_EMPTY";
    public const string FileTooLarge = "FILE_TOO_LARGE";
    public const string FileTypeUnsupported = "FILE_TYPE_UNSUPPORTED";
    public const string FileExtensionUnsupported = "FILE_EXTENSION_UNSUPPORTED";
    public const string FileExtensionMismatch = "FILE_EXTENSION_MISMATCH";
    public const string FileContentTypeMismatch = "FILE_CONTENT_TYPE_MISMATCH";
}

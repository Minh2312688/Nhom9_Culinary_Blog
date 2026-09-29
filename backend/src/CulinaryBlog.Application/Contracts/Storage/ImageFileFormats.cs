namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Metadata và luật validate của các định dạng ảnh được chấp nhận (CONS-007):
/// tối đa 5 MB, chỉ JPEG/PNG/WebP/AVIF, kiểm tra bằng magic bytes thay vì tin extension.
/// </summary>
public static class ImageFileFormats
{
    /// <summary>5 MiB (5 * 1024 * 1024 byte), khớp client_max_body_size 5m của reverse proxy.</summary>
    public const long MaxFileSizeBytes = 5L * 1024L * 1024L;

    /// <summary>Số byte đầu file dùng để dò magic bytes và AVIF compatible brand.</summary>
    public const int HeaderSniffLength = 32;

    public static readonly IReadOnlyList<ImageFileFormat> All = new[]
    {
        ImageFileFormat.Jpeg,
        ImageFileFormat.Png,
        ImageFileFormat.WebP,
        ImageFileFormat.Avif,
    };

    public static readonly IReadOnlyList<string> AllowedExtensions = new[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".avif",
    };

    public static readonly IReadOnlyList<string> AllowedContentTypes = new[]
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/avif",
    };

    /// <summary>Extension canonical dùng khi đặt tên object trên object storage.</summary>
    public static string GetCanonicalExtension(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => ".jpg",
        ImageFileFormat.Png => ".png",
        ImageFileFormat.WebP => ".webp",
        ImageFileFormat.Avif => ".avif",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format."),
    };

    /// <summary>Content type canonical dùng khi lưu object.</summary>
    public static string GetContentType(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => "image/jpeg",
        ImageFileFormat.Png => "image/png",
        ImageFileFormat.WebP => "image/webp",
        ImageFileFormat.Avif => "image/avif",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format."),
    };

    /// <summary>Tên hiển thị dùng trong thông báo lỗi.</summary>
    public static string GetDisplayName(ImageFileFormat format) => format switch
    {
        ImageFileFormat.Jpeg => "JPEG",
        ImageFileFormat.Png => "PNG",
        ImageFileFormat.WebP => "WebP",
        ImageFileFormat.Avif => "AVIF",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported image format."),
    };

    public static bool IsAllowedExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension)
           && AllowedExtensions.Contains(extension.Trim().ToLowerInvariant());

    public static bool IsAllowedContentType(string? contentType)
        => !string.IsNullOrWhiteSpace(contentType)
           && AllowedContentTypes.Contains(contentType.Trim().ToLowerInvariant());

    /// <summary>Extension client khai báo có khớp định dạng phát hiện từ magic bytes hay không.</summary>
    public static bool IsExtensionMatch(ImageFileFormat format, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        var normalizedExtension = extension.Trim().ToLowerInvariant();

        return normalizedExtension == GetCanonicalExtension(format)
            || (format == ImageFileFormat.Jpeg && normalizedExtension == ".jpeg");
    }
}

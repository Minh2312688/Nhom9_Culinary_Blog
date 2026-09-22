using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Storage;

namespace CulinaryBlog.Application.Common.Files;

/// <summary>
/// Kiểm tra file ảnh trước khi lưu trữ: kích thước, magic bytes và tính nhất quán
/// giữa nội dung thật với extension/MIME client khai báo (CONS-007, NFR-SEC-004).
/// Không gọi object storage: đây là bước chuẩn bị cho FR-FILE-001 (MinIO foundation).
/// </summary>
public sealed class FileValidationService : IFileValidationService
{
    public FileValidationResult Validate(FileUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);

        if (!request.Content.CanSeek)
        {
            throw new ArgumentException(
                "Content stream must be seekable so magic bytes can be read without consuming the upload stream.",
                nameof(request));
        }

        var sizeBytes = ResolveSize(request);

        if (sizeBytes <= 0)
        {
            throw new InvalidFileException(
                FileErrorCodes.FileEmpty,
                "File is empty.");
        }

        if (sizeBytes > ImageFileFormats.MaxFileSizeBytes)
        {
            throw new InvalidFileException(
                FileErrorCodes.FileTooLarge,
                $"File size ({sizeBytes} bytes) exceeds the allowed maximum of {ImageFileFormats.MaxFileSizeBytes} bytes (5 MB).");
        }

        var header = ReadHeader(request.Content);
        var detectedFormat = ImageMagicBytes.Detect(header);

        if (detectedFormat is null)
        {
            throw new InvalidFileException(
                FileErrorCodes.FileTypeUnsupported,
                "File content does not match a supported image format (JPEG, PNG, WebP, AVIF).");
        }

        var extension = GetNormalizedExtension(request.FileName);

        if (!ImageFileFormats.IsAllowedExtension(extension))
        {
            throw new InvalidFileException(
                FileErrorCodes.FileExtensionUnsupported,
                $"File extension must be one of: {string.Join(", ", ImageFileFormats.AllowedExtensions)}.");
        }

        if (!ImageFileFormats.IsExtensionMatch(detectedFormat.Value, extension))
        {
            throw new InvalidFileException(
                FileErrorCodes.FileExtensionMismatch,
                $"File extension does not match the detected content format {ImageFileFormats.GetDisplayName(detectedFormat.Value)}.");
        }

        ValidateContentType(detectedFormat.Value, request.ContentType);

        return new FileValidationResult(
            detectedFormat.Value,
            ImageFileFormats.GetContentType(detectedFormat.Value),
            ImageFileFormats.GetCanonicalExtension(detectedFormat.Value),
            sizeBytes);
    }

    /// <summary>
    /// Stream là nguồn tin cậy: client có thể khai báo Length nhỏ hơn kích thước thật,
    /// nên dùng giá trị lớn hơn giữa metadata và stream.
    /// Caller đã đảm bảo stream là seekable trước khi gọi helper này.
    /// </summary>
    private static long ResolveSize(FileUploadRequest request)
        => Math.Max(request.Length, request.Content.Length);

    private static void ValidateContentType(ImageFileFormat format, string? contentType)
    {
        // Client có thể không gửi MIME: magic bytes mới là nguồn tin cậy.
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return;
        }

        var isMatchingContentType = string.Equals(
            contentType.Trim(),
            ImageFileFormats.GetContentType(format),
            StringComparison.OrdinalIgnoreCase);

        if (!isMatchingContentType || !ImageFileFormats.IsAllowedContentType(contentType))
        {
            throw new InvalidFileException(
                FileErrorCodes.FileContentTypeMismatch,
                $"Declared content type does not match the detected content format {ImageFileFormats.GetDisplayName(format)}.");
        }
    }

    private static string GetNormalizedExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var extension = Path.GetExtension(fileName);

        return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Đọc tối đa HeaderSniffLength byte từ đầu file rồi khôi phục vị trí stream
    /// để caller (upload MinIO) vẫn đọc được toàn bộ nội dung.
    /// </summary>
    private static byte[] ReadHeader(Stream content)
    {
        var originalPosition = content.Position;
        var buffer = new byte[ImageFileFormats.HeaderSniffLength];

        try
        {
            content.Position = 0;

            var totalRead = 0;

            while (totalRead < buffer.Length)
            {
                var read = content.Read(buffer, totalRead, buffer.Length - totalRead);

                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead == buffer.Length)
            {
                return buffer;
            }

            var truncated = new byte[totalRead];
            Array.Copy(buffer, truncated, totalRead);

            return truncated;
        }
        finally
        {
            content.Position = originalPosition;
        }
    }
}

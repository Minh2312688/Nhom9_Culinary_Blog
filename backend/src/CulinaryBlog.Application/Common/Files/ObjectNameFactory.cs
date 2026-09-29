using CulinaryBlog.Application.Contracts.Storage;

namespace CulinaryBlog.Application.Common.Files;

/// <summary>
/// Sinh object name duy nhất cho object storage (FR-FILE-001 yêu cầu {Guid}{ext}).
/// Tên không chứa bất kỳ text do client cung cấp nên không thể path traversal,
/// và extension luôn lấy từ định dạng phát hiện bằng magic bytes.
/// </summary>
public static class ObjectNameFactory
{
    public static string Create(string folder, ImageFileFormat format)
    {
        if (!StorageFolders.IsValid(folder))
        {
            throw new ArgumentException(
                "Folder must contain only lowercase letters, digits, dashes and slash separators.",
                nameof(folder));
        }

        return $"{folder.Trim()}/{Guid.NewGuid():N}{ImageFileFormats.GetCanonicalExtension(format)}";
    }
}

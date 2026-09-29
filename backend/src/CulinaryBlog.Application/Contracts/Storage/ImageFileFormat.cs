namespace CulinaryBlog.Application.Contracts.Storage;

/// <summary>
/// Định dạng ảnh được chấp nhận upload theo CONS-007.
/// </summary>
public enum ImageFileFormat
{
    Jpeg = 1,
    Png = 2,
    WebP = 3,
    Avif = 4,
}

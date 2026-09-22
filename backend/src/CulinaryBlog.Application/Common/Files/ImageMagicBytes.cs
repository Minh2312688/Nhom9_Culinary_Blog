using CulinaryBlog.Application.Contracts.Storage;

namespace CulinaryBlog.Application.Common.Files;

/// <summary>
/// Dò định dạng ảnh từ magic bytes - nguồn tin cậy duy nhất khi chấp nhận file.
/// So sánh bằng giá trị byte để không phụ thuộc encoding của tên định dạng.
/// </summary>
internal static class ImageMagicBytes
{
    private const int IsoBmffBoxTypeOffset = 4;
    private const int IsoBmffMajorBrandOffset = 8;
    private const int IsoBmffCompatibleBrandOffset = 16;
    private const int WebPChunkTypeOffset = 8;

    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] RiffSignature = { 0x52, 0x49, 0x46, 0x46 };
    private static readonly byte[] WebPChunkType = { 0x57, 0x45, 0x42, 0x50 };
    private static readonly byte[] IsoBmffBoxType = { 0x66, 0x74, 0x79, 0x70 };
    private static readonly byte[] AvifBrand = { 0x61, 0x76, 0x69, 0x66 };
    private static readonly byte[] AvisBrand = { 0x61, 0x76, 0x69, 0x73 };

    /// <summary>Trả về định dạng phát hiện được, hoặc null nếu header không khớp định dạng được hỗ trợ.</summary>
    public static ImageFileFormat? Detect(byte[] header)
    {
        if (MatchesAt(header, 0, PngSignature))
        {
            return ImageFileFormat.Png;
        }

        if (MatchesAt(header, 0, JpegSignature))
        {
            return ImageFileFormat.Jpeg;
        }

        if (MatchesAt(header, 0, RiffSignature) && MatchesAt(header, WebPChunkTypeOffset, WebPChunkType))
        {
            return ImageFileFormat.WebP;
        }

        if (MatchesAt(header, IsoBmffBoxTypeOffset, IsoBmffBoxType) && IsAvifBrand(header))
        {
            return ImageFileFormat.Avif;
        }

        return null;
    }

    /// <summary>AVIF: major brand avif/avis, hoặc compatible brand avif trong cửa sổ header đã đọc.</summary>
    private static bool IsAvifBrand(byte[] header)
    {
        if (MatchesAt(header, IsoBmffMajorBrandOffset, AvifBrand) || MatchesAt(header, IsoBmffMajorBrandOffset, AvisBrand))
        {
            return true;
        }

        for (var offset = IsoBmffCompatibleBrandOffset; offset + AvifBrand.Length <= header.Length; offset += 4)
        {
            if (MatchesAt(header, offset, AvifBrand))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAt(byte[] header, int offset, byte[] signature)
    {
        if (offset < 0 || offset + signature.Length > header.Length)
        {
            return false;
        }

        for (var index = 0; index < signature.Length; index++)
        {
            if (header[offset + index] != signature[index])
            {
                return false;
            }
        }

        return true;
    }
}

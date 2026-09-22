using CulinaryBlog.Application.Contracts.Storage;

namespace CulinaryBlog.Application.Tests.Common.Files;

/// <summary>
/// Byte mẫu tối thiểu cho unit test magic bytes. Unit test chỉ kiểm tra header
/// nên không cần file ảnh thật và không cần thư viện xử lý ảnh.
/// </summary>
internal static class TestImageFixtures
{
    public static readonly byte[] Jpeg = BuildJpeg();
    public static readonly byte[] Png = BuildPng();
    public static readonly byte[] WebP = BuildWebP();
    public static readonly byte[] Avif = BuildAvif();
    public static readonly byte[] AvifWithAvisMajorBrand = BuildAvif("avis");
    public static readonly byte[] AvifWithMif1MajorBrandAndAvifCompatibleBrand = BuildAvif("mif1", "avif");
    public static readonly byte[] Executable = BuildExecutable();
    public static readonly byte[] Pdf = BuildPdf();
    public static readonly byte[] Gif = BuildGif();
    public static readonly byte[] PlainText = BuildPlainText();
    public static readonly byte[] RiffWave = BuildRiffWave();
    public static readonly byte[] Mp4 = BuildMp4();
    public static readonly byte[] TruncatedJpeg = { 0xFF, 0xD8 };

    public static byte[] BuildJpeg(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 20)];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;
        bytes[4] = 0x00;
        bytes[5] = 0x10;

        return bytes;
    }

    public static byte[] BuildPaddedJpeg(long totalLength)
        => BuildJpeg(checked((int)totalLength));

    public static byte[] BuildPng(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        var signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Array.Copy(signature, bytes, signature.Length);

        return bytes;
    }

    public static byte[] BuildWebP(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        WriteAscii(bytes, 0, "RIFF");
        WriteAscii(bytes, 8, "WEBP");

        return bytes;
    }

    public static byte[] BuildAvif(string majorBrand = "avif", string? compatibleBrand = null, int totalLength = 32)
    {
        var bytes = new byte[Math.Max(totalLength, 32)];
        bytes[3] = 0x20;
        WriteAscii(bytes, 4, "ftyp");
        WriteAscii(bytes, 8, majorBrand);

        if (compatibleBrand is not null)
        {
            WriteAscii(bytes, 16, compatibleBrand);
        }

        return bytes;
    }

    public static byte[] BuildExecutable(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        WriteAscii(bytes, 0, "MZ");

        return bytes;
    }

    public static byte[] BuildPdf(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        WriteAscii(bytes, 0, "%PDF-1.7");

        return bytes;
    }

    public static byte[] BuildGif(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        WriteAscii(bytes, 0, "GIF89a");

        return bytes;
    }

    public static byte[] BuildPlainText(int totalLength = 32)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        WriteAscii(bytes, 0, "khong phai file anh");

        return bytes;
    }

    public static byte[] BuildRiffWave(int totalLength = 64)
    {
        var bytes = new byte[Math.Max(totalLength, 16)];
        WriteAscii(bytes, 0, "RIFF");
        WriteAscii(bytes, 8, "WAVE");

        return bytes;
    }

    public static byte[] BuildMp4(int totalLength = 32)
    {
        var bytes = new byte[Math.Max(totalLength, 32)];
        bytes[3] = 0x20;
        WriteAscii(bytes, 4, "ftyp");
        WriteAscii(bytes, 8, "mp42");

        return bytes;
    }

    public static FileUploadRequest CreateRequest(
        byte[] content,
        string? fileName,
        string? contentType = null,
        long? declaredLength = null,
        string folder = StorageFolders.Categories)
        => new(
            new MemoryStream(content),
            fileName ?? string.Empty,
            contentType,
            declaredLength ?? content.LongLength,
            folder);

    private static void WriteAscii(byte[] target, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            target[offset + index] = (byte)value[index];
        }
    }
}

using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Common.Files;

/// <summary>
/// Unit test cho FileValidationService (TV4-02 MinIO foundation).
/// Không cần MinIO: chỉ kiểm tra size, magic bytes, extension và MIME.
/// </summary>
public class FileValidationServiceTests
{
    private readonly FileValidationService _service = new();

    [Fact]
    public void Validate_ValidJpeg_ShouldReturnDetectedFormat()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Jpeg, "photo.jpg", "image/jpeg");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Jpeg);
        result.ContentType.Should().Be("image/jpeg");
        result.Extension.Should().Be(".jpg");
        result.SizeBytes.Should().Be(TestImageFixtures.Jpeg.Length);
    }

    [Fact]
    public void Validate_ValidPng_ShouldReturnDetectedFormat()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Png, "photo.png", "image/png");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Png);
        result.ContentType.Should().Be("image/png");
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public void Validate_ValidWebP_ShouldReturnDetectedFormat()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.WebP, "photo.webp", "image/webp");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.WebP);
        result.ContentType.Should().Be("image/webp");
        result.Extension.Should().Be(".webp");
    }

    [Fact]
    public void Validate_ValidAvif_ShouldReturnDetectedFormat()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Avif, "photo.avif", "image/avif");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Avif);
        result.ContentType.Should().Be("image/avif");
        result.Extension.Should().Be(".avif");
    }

    [Fact]
    public void Validate_AvifWithAvisMajorBrand_ShouldBeAccepted()
    {
        // Arrange: major brand avis vẫn là ảnh AVIF hợp lệ
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.AvifWithAvisMajorBrand, "photo.avif", "image/avif");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Avif);
    }

    [Fact]
    public void Validate_AvifWithMif1MajorBrandAndAvifCompatibleBrand_ShouldBeAccepted()
    {
        // Arrange: major brand mif1 nhưng compatible brand avif
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.AvifWithMif1MajorBrandAndAvifCompatibleBrand, "photo.avif", "image/avif");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Avif);
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.JPEG")]
    [InlineData("photo.jPeG")]
    public void Validate_UppercaseOrAlternateJpegExtension_ShouldBeAccepted(string fileName)
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Jpeg, fileName, "image/jpeg");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Jpeg);
    }

    [Fact]
    public void Validate_MissingContentType_ShouldBeAccepted()
    {
        // Arrange: client không gửi MIME, magic bytes là nguồn tin cậy
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Png, "photo.png");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.Format.Should().Be(ImageFileFormat.Png);
    }

    [Fact]
    public void Validate_SizeExactlyAtLimit_ShouldBeAccepted()
    {
        // Arrange: đúng 5 MiB là hợp lệ, chỉ lớn hơn mới bị từ chối
        var content = TestImageFixtures.BuildPaddedJpeg(ImageFileFormats.MaxFileSizeBytes);
        var request = TestImageFixtures.CreateRequest(content, "photo.jpg", "image/jpeg");

        // Act
        var result = _service.Validate(request);

        // Assert
        result.SizeBytes.Should().Be(ImageFileFormats.MaxFileSizeBytes);
    }

    [Fact]
    public void Validate_ShouldNotConsumeOrDisposeCallerStream()
    {
        // Arrange
        var content = TestImageFixtures.Jpeg;
        var request = TestImageFixtures.CreateRequest(content, "photo.jpg", "image/jpeg");

        // Act
        _service.Validate(request);

        // Assert: upload MinIO sau đó vẫn phải đọc được toàn bộ nội dung từ đầu
        request.Content.Position.Should().Be(0);
        request.Content.CanRead.Should().BeTrue();

        var buffer = new byte[content.Length];
        request.Content.Read(buffer, 0, buffer.Length).Should().Be(content.Length);
        buffer.Should().Equal(content);
    }

    [Fact]
    public void Validate_EmptyFile_ShouldBeRejectedAsFileEmpty()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(Array.Empty<byte>(), "photo.jpg", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileEmpty);
    }

    [Fact]
    public void Validate_FileOneByteOverLimit_ShouldBeRejectedAsFileTooLarge()
    {
        // Arrange
        var content = TestImageFixtures.BuildPaddedJpeg(ImageFileFormats.MaxFileSizeBytes + 1);
        var request = TestImageFixtures.CreateRequest(content, "photo.jpg", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTooLarge);
    }

    [Fact]
    public void Validate_LargeFileWithValidJpegHeader_ShouldBeRejectedAsFileTooLarge()
    {
        // Arrange: 6 MiB nhưng header JPEG hợp lệ vẫn phải bị chặn theo CONS-007
        var content = TestImageFixtures.BuildPaddedJpeg(6L * 1024L * 1024L);
        var request = TestImageFixtures.CreateRequest(content, "photo.jpg", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTooLarge);
    }

    [Fact]
    public void Validate_DeclaredLengthSmallerThanActualStream_ShouldBeRejectedAsFileTooLarge()
    {
        // Arrange: client khai báo 10 byte nhưng stream thật vượt quá 5 MiB
        var content = TestImageFixtures.BuildPaddedJpeg(ImageFileFormats.MaxFileSizeBytes + 1);
        var request = TestImageFixtures.CreateRequest(content, "photo.jpg", "image/jpeg", declaredLength: 10);

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTooLarge);
    }

    [Theory]
    [InlineData("photo.gif")]
    [InlineData("photo.exe")]
    [InlineData("photo.txt")]
    [InlineData("photo.jpg.exe")]
    [InlineData("photo")]
    [InlineData(null)]
    public void Validate_UnsupportedExtension_ShouldBeRejectedAsExtensionUnsupported(string? fileName)
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Jpeg, fileName, "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileExtensionUnsupported);
    }

    [Fact]
    public void Validate_PngContentWithJpgExtension_ShouldBeRejectedAsExtensionMismatch()
    {
        // Arrange: nội dung PNG nhưng đổi tên .jpg
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Png, "photo.jpg", "image/png");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileExtensionMismatch);
    }

    [Fact]
    public void Validate_JpegContentWithPngExtension_ShouldBeRejectedAsExtensionMismatch()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Jpeg, "photo.png", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileExtensionMismatch);
    }

    [Fact]
    public void Validate_WebPContentWithAvifExtension_ShouldBeRejectedAsExtensionMismatch()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.WebP, "photo.avif", "image/webp");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileExtensionMismatch);
    }

    [Fact]
    public void Validate_PngContentWithJpegContentType_ShouldBeRejectedAsContentTypeMismatch()
    {
        // Arrange: extension đúng nhưng MIME client khai báo sai
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Png, "photo.png", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileContentTypeMismatch);
    }

    [Fact]
    public void Validate_UnsupportedContentType_ShouldBeRejectedAsContentTypeMismatch()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.Jpeg, "photo.jpg", "application/octet-stream");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileContentTypeMismatch);
    }

    [Fact]
    public void Validate_ExecutableRenamedToJpg_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange: file .exe đổi tên thành .jpg (acceptance criteria của TV4)
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Executable, "photo.jpg", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_PdfRenamedToPng_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Pdf, "photo.png", "image/png");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_PlainTextRenamedToJpg_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.PlainText, "photo.jpg", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_GifRenamedToPng_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange: GIF là ảnh nhưng không nằm trong danh sách định dạng của CONS-007
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Gif, "photo.png", "image/png");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_TruncatedJpeg_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange: chỉ có 2 byte FF D8, thiếu byte thứ ba của magic bytes JPEG
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.TruncatedJpeg, "photo.jpg", "image/jpeg");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_RiffWaveRenamedToWebP_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange: RIFF container nhưng chunk type là WAVE, không phải WEBP
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.RiffWave, "photo.webp", "image/webp");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_Mp4RenamedToAvif_ShouldBeRejectedAsTypeUnsupported()
    {
        // Arrange: ISO-BMFF với brand mp42, không phải avif/avis
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Mp4, "photo.avif", "image/avif");

        // Act & Assert
        ShouldReject(request, FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_NullRequest_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => _service.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_NullContent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var request = new FileUploadRequest(null!, "photo.jpg", "image/jpeg", 64, StorageFolders.Categories);

        // Act
        var act = () => _service.Validate(request);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_NonSeekableStream_ShouldThrowArgumentException()
    {
        // Arrange: không thể đọc magic bytes mà không tiêu thụ stream
        var request = new FileUploadRequest(
            new NonSeekableStream(TestImageFixtures.Jpeg),
            "photo.jpg",
            "image/jpeg",
            TestImageFixtures.Jpeg.Length,
            StorageFolders.Categories);

        // Act
        var act = () => _service.Validate(request);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private void ShouldReject(FileUploadRequest request, string expectedCode)
    {
        var act = () => _service.Validate(request);

        var exception = act.Should().Throw<InvalidFileException>().Which;
        exception.Code.Should().Be(expectedCode);
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream _inner;

        public NonSeekableStream(byte[] content)
        {
            _inner = new MemoryStream(content);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

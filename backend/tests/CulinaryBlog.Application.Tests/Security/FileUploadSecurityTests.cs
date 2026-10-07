using System.Text.RegularExpressions;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.Tests.Common.Files;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Security;

/// <summary>
/// NFR-SEC-004: File upload security test suite.
/// Validates:
/// A. Fake extension/content-type with invalid magic bytes is rejected.
/// B. File size > 5MB limit is rejected before storage processing.
/// C. Malicious client filenames (e.g. ../../shell.php) never determine storage paths;
///    paths use GUID + canonical extension and path traversal in folder names is blocked.
/// D. Supported real image signatures (JPEG, PNG, WebP, AVIF) remain accepted.
/// </summary>
public class FileUploadSecurityTests
{
    private readonly FileValidationService _validationService = new();

    #region Requirement A: Magic-Byte Validation & Spoofing Prevention

    [Fact]
    public void Validate_ExecutableWithJpegExtensionAndMime_ShouldBeRejected()
    {
        // Fake file: executable bytes disguised as photo.jpg with image/jpeg MIME
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.Executable, "malicious.jpg", "image/jpeg");

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_PdfDisguisedAsPng_ShouldBeRejected()
    {
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.Pdf, "document.png", "image/png");

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_PlainTextDisguisedAsWebp_ShouldBeRejected()
    {
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.PlainText, "payload.webp", "image/webp");

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_TruncatedOrCorruptHeader_ShouldBeRejected()
    {
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.TruncatedJpeg, "corrupt.jpg", "image/jpeg");

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTypeUnsupported);
    }

    [Fact]
    public void Validate_ContentAndExtensionMismatch_ShouldBeRejected()
    {
        // Real PNG content disguised as .jpg
        var request = TestImageFixtures.CreateRequest(
            TestImageFixtures.Png, "photo.jpg", "image/png");

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileExtensionMismatch);
    }

    #endregion

    #region Requirement B: File Size Validation Before Full Stream Read

    [Fact]
    public void Validate_FileExceeding5MbLimit_ShouldBeRejectedAsFileTooLarge()
    {
        // File 1 byte over 5MB (5,242,881 bytes)
        var content = TestImageFixtures.BuildPaddedJpeg(ImageFileFormats.MaxFileSizeBytes + 1);
        var request = TestImageFixtures.CreateRequest(content, "large.jpg", "image/jpeg");

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTooLarge);
    }

    [Fact]
    public void Validate_DeclaredLengthExceeding5MbLimit_ShouldBeRejected()
    {
        var content = TestImageFixtures.Jpeg;
        var request = TestImageFixtures.CreateRequest(
            content, "photo.jpg", "image/jpeg", declaredLength: ImageFileFormats.MaxFileSizeBytes + 1024);

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTooLarge);
    }

    [Fact]
    public void Validate_StreamLongerThanDeclaredLengthOverLimit_ShouldBeRejected()
    {
        var content = TestImageFixtures.BuildPaddedJpeg(ImageFileFormats.MaxFileSizeBytes + 100);
        // Client lied about small length, but actual stream exceeds limit
        var request = TestImageFixtures.CreateRequest(
            content, "deceptive.jpg", "image/jpeg", declaredLength: 100);

        var act = () => _validationService.Validate(request);

        var ex = act.Should().Throw<InvalidFileException>().Which;
        ex.Code.Should().Be(FileErrorCodes.FileTooLarge);
    }

    [Fact]
    public void Validate_FileAtExact5MbLimit_ShouldBeAccepted()
    {
        var content = TestImageFixtures.BuildPaddedJpeg(ImageFileFormats.MaxFileSizeBytes);
        var request = TestImageFixtures.CreateRequest(content, "limit.jpg", "image/jpeg");

        var result = _validationService.Validate(request);

        result.SizeBytes.Should().Be(ImageFileFormats.MaxFileSizeBytes);
    }

    #endregion

    #region Requirement C: GUID Filenames & Path Traversal Protection

    [Theory]
    [InlineData("../../shell.php")]
    [InlineData("..\\..\\malicious.jpg")]
    [InlineData("../../../etc/passwd")]
    [InlineData("C:\\inetpub\\wwwroot\\exploit.aspx")]
    [InlineData("photo.jpg\0.exe")]
    public void ObjectName_MaliciousClientFilename_NeverBecomesStoragePath(string clientFilename)
    {
        // Client filename is completely ignored when generating storage object names
        var objectName = ObjectNameFactory.Create(StorageFolders.Categories, ImageFileFormat.Jpeg);

        // Path must never contain the client's provided filename, traversal markers, or malicious extensions
        objectName.Should().NotContain(clientFilename);
        objectName.Should().NotContain("shell");
        objectName.Should().NotContain("passwd");
        objectName.Should().NotContain("exploit");
        objectName.Should().NotContain("..");
        objectName.Should().StartWith("categories/");
        objectName.Should().EndWith(".jpg");

        // Object name must strictly match GUID pattern
        Regex.IsMatch(objectName, @"^categories/[0-9a-f]{32}\.jpg$").Should().BeTrue();
    }

    [Theory]
    [InlineData("../../categories")]
    [InlineData("categories/..")]
    [InlineData("../recipes/evil")]
    [InlineData("/root/categories")]
    public void ObjectName_PathTraversalInFolder_ShouldBeRejected(string maliciousFolder)
    {
        var act = () => ObjectNameFactory.Create(maliciousFolder, ImageFileFormat.Jpeg);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Requirement D: Supported Formats Accepted

    [Fact]
    public void Validate_SupportedRealJpeg_ShouldBeAccepted()
    {
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Jpeg, "valid.jpg", "image/jpeg");
        var result = _validationService.Validate(request);

        result.Format.Should().Be(ImageFileFormat.Jpeg);
        result.Extension.Should().Be(".jpg");
    }

    [Fact]
    public void Validate_SupportedRealPng_ShouldBeAccepted()
    {
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Png, "valid.png", "image/png");
        var result = _validationService.Validate(request);

        result.Format.Should().Be(ImageFileFormat.Png);
        result.Extension.Should().Be(".png");
    }

    [Fact]
    public void Validate_SupportedRealWebP_ShouldBeAccepted()
    {
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.WebP, "valid.webp", "image/webp");
        var result = _validationService.Validate(request);

        result.Format.Should().Be(ImageFileFormat.WebP);
        result.Extension.Should().Be(".webp");
    }

    [Fact]
    public void Validate_SupportedRealAvif_ShouldBeAccepted()
    {
        var request = TestImageFixtures.CreateRequest(TestImageFixtures.Avif, "valid.avif", "image/avif");
        var result = _validationService.Validate(request);

        result.Format.Should().Be(ImageFileFormat.Avif);
        result.Extension.Should().Be(".avif");
    }

    #endregion
}

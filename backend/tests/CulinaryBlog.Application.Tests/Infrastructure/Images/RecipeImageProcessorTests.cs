using FluentAssertions;
using SkiaSharp;
using Xunit;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Images;

namespace CulinaryBlog.Application.Tests.Infrastructure.Images;

public sealed class RecipeImageProcessorTests
{
    [Theory]
    [InlineData(300, 300)]
    [InlineData(800, 600)]
    public async Task CreateVariantsAsync_ProducesExactCenterCroppedDimensions(int width, int height)
    {
        using var source = CreateWideTestImageStream();
        var variants = await new RecipeImageProcessor().CreateVariantsAsync(
            source, CancellationToken.None);

        variants.Status.Should().Be(RecipeImageVariantStatus.Ready);
        using var output = SKBitmap.Decode(width == 300 ? variants.Thumbnail : variants.Medium);
        output.Should().NotBeNull();
        output!.Width.Should().Be(width);
        output.Height.Should().Be(height);
        AssertGreen(output.GetPixel(0, 0));
        AssertGreen(output.GetPixel(width - 1, height - 1));
    }

    [Theory]
    [MemberData(nameof(SupportedRasterInputs))]
    public async Task CreateVariantsAsync_ProducesPngForSupportedRasterFormats(byte[] sourceBytes)
    {
        using var source = new MemoryStream(sourceBytes);

        var variants = await new RecipeImageProcessor().CreateVariantsAsync(
            source, CancellationToken.None);

        variants.Status.Should().Be(RecipeImageVariantStatus.Ready);
        variants.ContentType.Should().Be("image/png");
        variants.Extension.Should().Be(".png");
        using var thumbnail = SKBitmap.Decode(variants.Thumbnail);
        using var medium = SKBitmap.Decode(variants.Medium);
        thumbnail.Should().NotBeNull();
        medium.Should().NotBeNull();
        thumbnail!.Width.Should().Be(300);
        thumbnail.Height.Should().Be(300);
        medium!.Width.Should().Be(800);
        medium.Height.Should().Be(600);
    }

    [Fact]
    public async Task CreateVariantsAsync_ReportsAvifUnsupportedWithoutChangingOriginal()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", "fruits_2layer_thumbsize.avif");
        using var source = File.OpenRead(fixturePath);
        var originalBytes = await File.ReadAllBytesAsync(fixturePath);

        var variants = await new RecipeImageProcessor().CreateVariantsAsync(
            source, CancellationToken.None);

        variants.Status.Should().Be(RecipeImageVariantStatus.Unsupported);
        variants.Thumbnail.Should().BeEmpty();
        variants.Medium.Should().BeEmpty();
        source.CanRead.Should().BeTrue();
        source.Position.Should().Be(0);
        await using var originalCopy = new MemoryStream();
        await source.CopyToAsync(originalCopy);
        originalCopy.ToArray().Should().Equal(originalBytes);
    }

    [Fact]
    public async Task CreateVariantsAsync_DoesNotDisposeOrRepositionSourceStream()
    {
        using var source = CreateWideTestImageStream();
        source.Position = 5;

        await new RecipeImageProcessor().CreateVariantsAsync(source, CancellationToken.None);

        source.CanRead.Should().BeTrue();
        source.Position.Should().Be(5);
    }

    [Fact]
    public async Task CreateVariantsAsync_RejectsInvalidImageData()
    {
        using var source = new MemoryStream([1, 2, 3, 4]);

        var act = async () => await new RecipeImageProcessor()
            .CreateVariantsAsync(source, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    public static TheoryData<byte[]> SupportedRasterInputs => new()
    {
        CreateImageBytes(SKEncodedImageFormat.Jpeg),
        CreateImageBytes(SKEncodedImageFormat.Png),
        CreateImageBytes(SKEncodedImageFormat.Webp)
    };

    private static MemoryStream CreateWideTestImageStream()
        => new(CreateImageBytes(SKEncodedImageFormat.Png));

    private static byte[] CreateImageBytes(SKEncodedImageFormat format)
    {
        using var bitmap = CreateWideBitmap();
        using var data = bitmap.Encode(format, 100);
        using var stream = new MemoryStream();
        data.SaveTo(stream);
        return stream.ToArray();
    }

    private static SKBitmap CreateWideBitmap()
    {
        var bitmap = new SKBitmap(new SKImageInfo(800, 200, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                bitmap.SetPixel(x, y, x < 200 ? SKColors.Red : x < 600 ? SKColors.Lime : SKColors.Blue);
            }
        }

        return bitmap;
    }

    private static void AssertGreen(SKColor pixel)
    {
        pixel.Red.Should().BeLessThan((byte)10);
        pixel.Green.Should().BeGreaterThan((byte)200);
        pixel.Blue.Should().BeLessThan((byte)10);
    }
}

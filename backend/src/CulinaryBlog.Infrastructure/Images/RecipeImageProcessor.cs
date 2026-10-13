using SkiaSharp;
using CulinaryBlog.Domain.Entities;

namespace CulinaryBlog.Infrastructure.Images;

public sealed record RecipeImageVariants(
    byte[] Thumbnail,
    byte[] Medium,
    string ContentType,
    string Extension,
    RecipeImageVariantStatus Status);

public sealed class RecipeImageProcessor
{
    private static readonly SKSizeI ThumbnailSize = new(300, 300);
    private static readonly SKSizeI MediumSize = new(800, 600);

    public async Task<RecipeImageVariants> CreateVariantsAsync(
        Stream original,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!original.CanRead)
        {
            throw new ArgumentException("The original image stream must be readable.", nameof(original));
        }

        var originalPosition = original.CanSeek ? original.Position : (long?)null;
        try
        {
            if (original.CanSeek)
            {
                original.Position = 0;
            }

            using var source = new MemoryStream();
            await original.CopyToAsync(source, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var imageBytes = source.ToArray();
            if (IsAvif(imageBytes))
            {
                return new RecipeImageVariants([], [], string.Empty, string.Empty, RecipeImageVariantStatus.Unsupported);
            }

            using var bitmap = Decode(imageBytes);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                throw new InvalidDataException("The uploaded file is not a supported image.");
            }

            return new RecipeImageVariants(
                CreatePngVariant(bitmap, ThumbnailSize),
                CreatePngVariant(bitmap, MediumSize),
                "image/png",
                ".png",
                RecipeImageVariantStatus.Ready);
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                original.Position = originalPosition.Value;
            }
        }
    }

    private static byte[] CreatePngVariant(SKBitmap source, SKSizeI targetSize)
    {
        using var target = new SKBitmap(new SKImageInfo(
            targetSize.Width,
            targetSize.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Unpremul));

        var sourceAspectRatio = (float)source.Width / source.Height;
        var targetAspectRatio = (float)targetSize.Width / targetSize.Height;
        SKRect sourceRect;

        if (sourceAspectRatio > targetAspectRatio)
        {
            var cropWidth = source.Height * targetAspectRatio;
            var left = (source.Width - cropWidth) / 2f;
            sourceRect = new SKRect(left, 0, left + cropWidth, source.Height);
        }
        else
        {
            var cropHeight = source.Width / targetAspectRatio;
            var top = (source.Height - cropHeight) / 2f;
            sourceRect = new SKRect(0, top, source.Width, top + cropHeight);
        }

        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                source,
                sourceRect,
                new SKRect(0, 0, targetSize.Width, targetSize.Height),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            canvas.Flush();
        }

        using var image = SKImage.FromBitmap(target);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        if (encoded is null)
        {
            throw new InvalidDataException("The image variant could not be encoded as PNG.");
        }

        return encoded.ToArray();
    }

    private static SKBitmap? Decode(byte[] imageBytes)
    {
        try
        {
            return SKBitmap.Decode(imageBytes);
        }
        catch (ArgumentNullException exception)
        {
            throw new InvalidDataException("The uploaded file is not a supported image.", exception);
        }
    }

    private static bool IsAvif(ReadOnlySpan<byte> imageBytes)
    {
        if (imageBytes.Length < 12 ||
            !imageBytes.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            return false;
        }

        if (HasAvifBrand(imageBytes.Slice(8, 4)))
        {
            return true;
        }

        for (var offset = 16; offset + 4 <= Math.Min(imageBytes.Length, 32); offset += 4)
        {
            if (HasAvifBrand(imageBytes.Slice(offset, 4)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAvifBrand(ReadOnlySpan<byte> brand)
        => brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8);
}

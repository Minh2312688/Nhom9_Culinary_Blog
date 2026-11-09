using CulinaryBlog.Infrastructure.Persistence.Seed;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Persistence;

public sealed class SampleCategoryImageLoaderTests
{
    [Fact]
    public void Load_indexes_supported_images_by_exact_slug_stem()
    {
        var directory = CreateDirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "mon-canh.jpg"), [1]);
            File.WriteAllBytes(Path.Combine(directory, "mon-viet.webp"), [1]);
            File.WriteAllBytes(Path.Combine(directory, "unrelated.txt"), [1]);

            var result = SampleCategoryImageLoader.Load(directory);

            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "mon-canh.jpg")), result["mon-canh"]);
            Assert.Equal(Path.GetFullPath(Path.Combine(directory, "mon-viet.webp")), result["mon-viet"]);
            Assert.False(result.ContainsKey("unrelated"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_rejects_duplicate_slug_stems()
    {
        var directory = CreateDirectory();
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "mon-canh.jpg"), [1]);
            File.WriteAllBytes(Path.Combine(directory, "mon-canh.png"), [1]);

            Assert.Throws<InvalidDataException>(() => SampleCategoryImageLoader.Load(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sample-category-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

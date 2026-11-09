namespace CulinaryBlog.Infrastructure.Persistence.Seed;

public static class SampleCategoryImageLoader
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".webp", ".jpg", ".jpeg", ".png" };

    public static IReadOnlyDictionary<string, string> Load(string imagesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagesDirectory);

        var imagePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var imagePath in Directory.EnumerateFiles(imagesDirectory)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(imagePath)))
            {
                continue;
            }

            var slug = Path.GetFileNameWithoutExtension(imagePath);
            if (!imagePaths.TryAdd(slug, Path.GetFullPath(imagePath)))
            {
                throw new InvalidDataException(
                    $"Multiple category images use the slug '{slug}' in '{imagesDirectory}'.");
            }
        }

        return imagePaths;
    }
}

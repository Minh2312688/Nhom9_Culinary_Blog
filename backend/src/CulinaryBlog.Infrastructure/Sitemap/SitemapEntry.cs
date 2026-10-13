namespace CulinaryBlog.Infrastructure.Sitemap;

public sealed record SitemapEntry(
    string RelativePath,
    DateTime LastModifiedUtc,
    string ChangeFrequency,
    decimal Priority);

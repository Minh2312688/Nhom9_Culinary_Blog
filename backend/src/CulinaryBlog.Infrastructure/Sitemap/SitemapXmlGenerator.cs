using System.Globalization;
using System.Xml.Linq;

namespace CulinaryBlog.Infrastructure.Sitemap;

public sealed class SitemapXmlGenerator
{
    private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private readonly Uri publicBaseUri;

    public SitemapXmlGenerator(SitemapOptions options)
    {
        publicBaseUri = options.Validate();
    }

    public byte[] Generate(IEnumerable<SitemapEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                SitemapNamespace + "urlset",
                entries.Select(entry => new XElement(
                    SitemapNamespace + "url",
                    new XElement(SitemapNamespace + "loc", ResolveLocation(entry.RelativePath)),
                    new XElement(SitemapNamespace + "lastmod", entry.LastModifiedUtc.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new XElement(SitemapNamespace + "changefreq", entry.ChangeFrequency),
                    new XElement(SitemapNamespace + "priority", entry.Priority.ToString("0.0", CultureInfo.InvariantCulture))))));

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private string ResolveLocation(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || !relativePath.StartsWith('/'))
        {
            throw new ArgumentException("Sitemap entry paths must be non-empty absolute-path references.", nameof(relativePath));
        }

        var path = string.Join(
            '/',
            relativePath.TrimStart('/').Split('/').Select(Uri.EscapeDataString));
        var baseUri = new Uri($"{publicBaseUri.AbsoluteUri.TrimEnd('/')}/");
        var uri = new Uri(baseUri, path);
        return uri.AbsoluteUri;
    }
}

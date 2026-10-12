using System.Xml.Linq;
using CulinaryBlog.Infrastructure.Sitemap;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Sitemap;

public sealed class SitemapXmlGeneratorTests
{
    [Fact]
    public void Generate_EmitsAbsoluteLocationsAndRequiredSitemapFields()
    {
        var generator = new SitemapXmlGenerator(new SitemapOptions
        {
            PublicBaseUrl = "https://food.example.test/"
        });
        var generated = generator.Generate([
            new SitemapEntry("/recipes/soup & stew", DateTime.UtcNow, "weekly", 0.8m),
            new SitemapEntry("/categories/baking", DateTime.UtcNow, "weekly", 0.6m),
            new SitemapEntry("/", DateTime.UtcNow, "monthly", 0.5m)
        ]);
        var document = XDocument.Load(new MemoryStream(generated));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = document.Root!.Elements(ns + "url").ToArray();

        urls.Should().HaveCount(3);
        var locations = document.Descendants(ns + "loc").Select(node => node.Value).ToArray();
        locations.Should().Contain("https://food.example.test/recipes/soup%20%26%20stew");
        locations.Select(value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri is not null)
            .Should().OnlyContain(isAbsolute => isAbsolute);
        urls.Should().OnlyContain(url =>
            url.Element(ns + "lastmod") != null &&
            url.Element(ns + "changefreq") != null &&
            url.Element(ns + "priority") != null);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a uri")]
    [InlineData("ftp://food.example.test")]
    public void Constructor_RejectsInvalidPublicBaseUrl(string baseUrl)
    {
        var act = () => new SitemapXmlGenerator(new SitemapOptions { PublicBaseUrl = baseUrl });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*absolute HTTP or HTTPS*");
    }

    [Fact]
    public void Generate_WithNoEntries_ReturnsValidEmptySitemap()
    {
        var generator = new SitemapXmlGenerator(new SitemapOptions
        {
            PublicBaseUrl = "https://food.example.test"
        });

        var document = XDocument.Load(new MemoryStream(generator.Generate([])));

        document.Root!.Name.LocalName.Should().Be("urlset");
        document.Root.Elements().Should().BeEmpty();
    }
}

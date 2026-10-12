using CulinaryBlog.Infrastructure.Sitemap;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Sitemap;

public sealed class SitemapOptionsTests
{
    [Fact]
    public void Validate_WithValidBaseUrl_ReturnsUri()
    {
        var options = new SitemapOptions { PublicBaseUrl = "https://food.example.test" };

        options.Validate().AbsoluteUri.Should().Be("https://food.example.test/");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a uri")]
    [InlineData("ftp://food.example.test")]
    public void Validate_WithInvalidBaseUrl_Throws(string baseUrl)
    {
        var options = new SitemapOptions { PublicBaseUrl = baseUrl };

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PublicBaseUrl*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryResolvePublicSitemapUri_WhenUnconfigured_ReturnsNull(string? value)
    {
        var options = new SitemapOptions { PublicSitemapUrl = value! };

        options.TryResolvePublicSitemapUri().Should().BeNull();
    }

    [Fact]
    public void TryResolvePublicSitemapUri_WhenConfigured_ReturnsExactUri()
    {
        var options = new SitemapOptions { PublicSitemapUrl = "https://cdn.example.test/sitemap.xml" };

        options.TryResolvePublicSitemapUri()!.AbsoluteUri.Should().Be("https://cdn.example.test/sitemap.xml");
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://cdn.example.test/sitemap.xml")]
    [InlineData("sitemap.xml")]
    public void TryResolvePublicSitemapUri_WhenInvalid_ThrowsWithoutFallback(string value)
    {
        var options = new SitemapOptions { PublicSitemapUrl = value };

        var act = () => options.TryResolvePublicSitemapUri();

        act.Should().Throw<InvalidOperationException>().WithMessage("*PublicSitemapUrl*");
    }
}

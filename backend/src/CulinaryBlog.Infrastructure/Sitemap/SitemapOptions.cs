namespace CulinaryBlog.Infrastructure.Sitemap;

public sealed class SitemapOptions
{
    public const string SectionName = "Sitemap";

    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Exact public URL of the generated <c>sitemap.xml</c> that Google Ping should
    /// receive. This is intentionally separate from <see cref="PublicBaseUrl"/>: the
    /// base URL is only used to build the website <c>&lt;loc&gt;</c> values and is not
    /// guaranteed to serve the sitemap file. When empty, the ping is skipped.
    /// </summary>
    public string PublicSitemapUrl { get; set; } = string.Empty;

    public Uri Validate()
    {
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "Sitemap configuration requires an absolute HTTP or HTTPS PublicBaseUrl without query or fragment.");
        }

        return uri;
    }

    /// <summary>
    /// Resolves the public sitemap URL used for the Google ping.
    /// </summary>
    /// <returns>
    /// The configured absolute HTTP/HTTPS URI, or <c>null</c> when
    /// <see cref="PublicSitemapUrl"/> is empty (the ping should be skipped).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a non-empty <see cref="PublicSitemapUrl"/> is not a valid absolute
    /// HTTP or HTTPS URL. There is no silent fallback to another URL.
    /// </exception>
    public Uri? TryResolvePublicSitemapUri()
    {
        if (string.IsNullOrWhiteSpace(PublicSitemapUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(PublicSitemapUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Sitemap configuration requires PublicSitemapUrl to be an absolute HTTP or HTTPS URL, or empty to skip the Google ping.");
        }

        return uri;
    }
}

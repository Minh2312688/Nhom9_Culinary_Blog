using System.Net;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Sitemap;

public sealed class GoogleSitemapPingClient
{
    private readonly HttpClient httpClient;
    private readonly ILogger<GoogleSitemapPingClient> logger;

    public GoogleSitemapPingClient(HttpClient httpClient, ILogger<GoogleSitemapPingClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task NotifyAsync(Uri sitemapUri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sitemapUri);
        var pingUri = new Uri(
            $"https://www.google.com/ping?sitemap={Uri.EscapeDataString(sitemapUri.AbsoluteUri)}");

        try
        {
            using var response = await httpClient.GetAsync(pingUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Google sitemap ping returned HTTP {StatusCode} for host {Host}.",
                    (int)response.StatusCode,
                    sitemapUri.Host);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Google sitemap ping timed out for host {Host}.", sitemapUri.Host);
        }
        catch (HttpRequestException exception)
        {
            logger.LogInformation(exception,
                "Google sitemap ping failed for host {Host}.", sitemapUri.Host);
        }
    }
}

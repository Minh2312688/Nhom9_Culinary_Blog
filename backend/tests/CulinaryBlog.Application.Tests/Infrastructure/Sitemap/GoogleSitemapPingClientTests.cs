using System.Net;
using CulinaryBlog.Infrastructure.Sitemap;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Sitemap;

public sealed class GoogleSitemapPingClientTests
{
    [Fact]
    public async Task NotifyAsync_RequestsGooglePingWithEscapedSitemapUrl()
    {
        Uri? requestedUri = null;
        using var httpClient = new HttpClient(new RecordingHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var client = new GoogleSitemapPingClient(httpClient, NullLogger<GoogleSitemapPingClient>.Instance);

        await client.NotifyAsync(new Uri("https://food.example.test/sitemap.xml"), CancellationToken.None);

        requestedUri!.Host.Should().Be("www.google.com");
        requestedUri.AbsolutePath.Should().Be("/ping");
        requestedUri.Query.Should().Contain(Uri.EscapeDataString("https://food.example.test/sitemap.xml"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task NotifyAsync_WhenGoogleReturnsError_CompletesWithoutThrowing(HttpStatusCode statusCode)
    {
        using var httpClient = new HttpClient(new RecordingHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(statusCode))));
        var client = new GoogleSitemapPingClient(httpClient, NullLogger<GoogleSitemapPingClient>.Instance);

        await client.NotifyAsync(new Uri("https://food.example.test/sitemap.xml"), CancellationToken.None);
    }

    [Fact]
    public async Task NotifyAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var httpClient = new HttpClient(new RecordingHandler(
            (_, token) => Task.FromCanceled<HttpResponseMessage>(token)));
        var client = new GoogleSitemapPingClient(httpClient, NullLogger<GoogleSitemapPingClient>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.NotifyAsync(new Uri("https://food.example.test/sitemap.xml"), cancellation.Token));
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}

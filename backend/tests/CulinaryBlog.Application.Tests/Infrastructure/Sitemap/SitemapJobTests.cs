using System.Net;
using System.Xml.Linq;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Sitemap;
using CulinaryBlog.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Sitemap;

public sealed class SitemapJobTests
{
    [Fact]
    public async Task GenerateAndPublishAsync_PublishesOnlyPublicRoutesAndPublishedRecipes()
    {
        await using var context = await CreateContextAsync();
        byte[]? uploadedXml = null;
        var storage = CreateStorage((_, _, content, _, _, _) =>
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            uploadedXml = copy.ToArray();
            return Task.CompletedTask;
        });
        var pingHandler = new PingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Gone)));
        using var httpClient = new HttpClient(pingHandler);
        var job = CreateJob(
            context,
            storage.Object,
            httpClient,
            publicSitemapUrl: "https://food.example.test/sitemap.xml");

        await job.GenerateAndPublishAsync(CancellationToken.None);

        var xml = XDocument.Load(new MemoryStream(uploadedXml!));
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locations = xml.Descendants(ns + "loc").Select(node => node.Value).ToArray();
        locations.Should().Contain("https://food.example.test/recipes/published-soup");
        locations.Should().Contain("https://food.example.test/categories/soups");
        locations.Should().Contain("https://food.example.test/");
        locations.Should().Contain("https://food.example.test/recipes");
        locations.Should().Contain("https://food.example.test/categories");
        locations.Should().Contain("https://food.example.test/search");
        locations.Should().NotContain(location => location.Contains("draft"));
        locations.Should().NotContain(location => location.Contains("dashboard"));
        locations.Should().NotContain(location => location.Contains("profile"));
        locations.Should().HaveCount(6);
        pingHandler.RequestUri!.AbsoluteUri.Should().Contain("www.google.com/ping");
        storage.Verify(client => client.UploadAsync(
            "culinary-blog",
            "sitemap.xml",
            It.IsAny<Stream>(),
            It.IsAny<long>(),
            "application/xml",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAndPublishAsync_WhenStorageFails_PropagatesForHangfireRetry()
    {
        await using var context = await CreateContextAsync();
        var storage = CreateStorage((_, _, _, _, _, _) => throw new IOException("storage unavailable"));
        using var httpClient = new HttpClient(new PingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var job = CreateJob(context, storage.Object, httpClient);

        var act = () => job.GenerateAndPublishAsync(CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public void Job_UsesTwoRetries()
    {
        var retry = typeof(SitemapJob)
            .GetCustomAttributes(typeof(Hangfire.AutomaticRetryAttribute), true)
            .Cast<Hangfire.AutomaticRetryAttribute>()
            .Single();

        retry.Attempts.Should().Be(2);
        retry.OnAttemptsExceeded.Should().Be(Hangfire.AttemptsExceededAction.Fail);
    }

    private static SitemapJob CreateJob(
        ApplicationDbContext context,
        IObjectStorageClient storage,
        HttpClient httpClient,
        string? publicSitemapUrl = null,
        ILogger<SitemapJob>? jobLogger = null)
    {
        var options = new SitemapOptions
        {
            PublicBaseUrl = "https://food.example.test",
            PublicSitemapUrl = publicSitemapUrl ?? string.Empty
        };
        return new SitemapJob(
            context,
            storage,
            new SitemapXmlGenerator(options),
            new GoogleSitemapPingClient(httpClient, NullLogger<GoogleSitemapPingClient>.Instance),
            options,
            "culinary-blog",
            jobLogger ?? NullLogger<SitemapJob>.Instance);
    }

    private static Mock<IObjectStorageClient> CreateStorage(
        Func<string, string, Stream, long, string, CancellationToken, Task> upload)
    {
        var storage = new Mock<IObjectStorageClient>();
        storage.Setup(client => client.EnsureBucketExistsAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(client => client.UploadAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<long>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(upload);
        return storage;
    }

    private static async Task<ApplicationDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"Sitemap_{Guid.NewGuid():N}")
            .Options;
        var context = new ApplicationDbContext(options);
        var category = Category.Create("Soups", null);
        var published = new Recipe
        {
            Title = "Published Soup",
            Slug = "published-soup",
            CategoryId = category.Id,
            AuthorId = "author",
            Status = RecipeStatus.Published
        };
        var draft = new Recipe
        {
            Title = "Draft Soup",
            Slug = "draft-soup",
            CategoryId = category.Id,
            AuthorId = "author",
            Status = RecipeStatus.Draft
        };
        context.Categories.Add(category);
        context.Recipes.AddRange(published, draft);
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task GenerateAndPublishAsync_WhenPublicSitemapUrlConfigured_PingsExactConfiguredUrl()
    {
        await using var context = await CreateContextAsync();
        byte[]? uploadedXml = null;
        var storage = CreateStorage((_, _, content, _, _, _) =>
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            uploadedXml = copy.ToArray();
            return Task.CompletedTask;
        });
        var pingHandler = new PingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = new HttpClient(pingHandler);
        const string configuredUrl = "https://cdn.example.test/assets/sitemap.xml";
        var job = CreateJob(context, storage.Object, httpClient, configuredUrl);

        await job.GenerateAndPublishAsync(CancellationToken.None);

        uploadedXml.Should().NotBeNull();
        pingHandler.CallCount.Should().Be(1);
        pingHandler.RequestUri!.AbsoluteUri.Should().Contain(Uri.EscapeDataString(configuredUrl));
        // The ping must use the configured URL and never derive it from PublicBaseUrl.
        pingHandler.RequestUri!.Query.Should()
            .NotContain(Uri.EscapeDataString("https://food.example.test/sitemap.xml"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateAndPublishAsync_WhenPublicSitemapUrlMissing_SkipsPingAndStillStoresSitemap(
        string publicSitemapUrl)
    {
        await using var context = await CreateContextAsync();
        byte[]? uploadedXml = null;
        var storage = CreateStorage((_, _, content, _, _, _) =>
        {
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            uploadedXml = copy.ToArray();
            return Task.CompletedTask;
        });
        var pingHandler = new PingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = new HttpClient(pingHandler);
        var jobLogger = new RecordingLogger<SitemapJob>();
        var job = CreateJob(context, storage.Object, httpClient, publicSitemapUrl, jobLogger);

        await job.GenerateAndPublishAsync(CancellationToken.None);

        uploadedXml.Should().NotBeNull();
        storage.Verify(client => client.UploadAsync(
            "culinary-blog",
            "sitemap.xml",
            It.IsAny<Stream>(),
            It.IsAny<long>(),
            "application/xml",
            It.IsAny<CancellationToken>()), Times.Once);
        pingHandler.CallCount.Should().Be(0);
        jobLogger.Entries.Should().Contain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("PublicSitemapUrl"));
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://food.example.test/sitemap.xml")]
    [InlineData("sitemap.xml")]
    public async Task GenerateAndPublishAsync_WhenPublicSitemapUrlInvalid_ThrowsBeforeAnyWork(
        string publicSitemapUrl)
    {
        await using var context = await CreateContextAsync();
        var storage = CreateStorage((_, _, _, _, _, _) =>
            throw new IOException("upload must not run when configuration is invalid"));
        var pingHandler = new PingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = new HttpClient(pingHandler);
        var job = CreateJob(context, storage.Object, httpClient, publicSitemapUrl);

        var act = () => job.GenerateAndPublishAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*PublicSitemapUrl*");
        pingHandler.CallCount.Should().Be(0);
        storage.Verify(client => client.UploadAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<long>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class PingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            RequestUri = request.RequestUri;
            return send(request, cancellationToken);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}

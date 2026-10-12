using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Storage;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Sitemap;

[AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Fail, LogEvents = true)]
public sealed class SitemapJob
{
    private static readonly (string Path, string Frequency, decimal Priority)[] StaticRoutes =
    [
        ("/", "monthly", 0.5m),
        ("/recipes", "monthly", 0.5m),
        ("/categories", "monthly", 0.5m),
        ("/search", "monthly", 0.5m)
    ];

    private readonly IApplicationDbContext context;
    private readonly IObjectStorageClient storage;
    private readonly SitemapXmlGenerator generator;
    private readonly GoogleSitemapPingClient pingClient;
    private readonly SitemapOptions options;
    private readonly string bucketName;
    private readonly ILogger<SitemapJob> logger;

    public SitemapJob(
        IApplicationDbContext context,
        IObjectStorageClient storage,
        SitemapXmlGenerator generator,
        GoogleSitemapPingClient pingClient,
        SitemapOptions options,
        string bucketName,
        ILogger<SitemapJob> logger)
    {
        this.context = context;
        this.storage = storage;
        this.generator = generator;
        this.pingClient = pingClient;
        this.options = options;
        this.bucketName = string.IsNullOrWhiteSpace(bucketName)
            ? throw new ArgumentException("Sitemap storage bucket is required.", nameof(bucketName))
            : bucketName;
        this.logger = logger;
    }

    public async Task GenerateAndPublishAsync(CancellationToken cancellationToken = default)
    {
        // Validate configuration before any work so a misconfigured value fails fast:
        // PublicBaseUrl builds the <loc> values and is required; PublicSitemapUrl is the
        // exact public URL of sitemap.xml that Google Ping should receive. A non-empty but
        // malformed PublicSitemapUrl is a configuration error (no silent fallback); an empty
        // one simply skips the ping while the sitemap is still generated and stored.
        options.Validate();
        var publicSitemapUri = options.TryResolvePublicSitemapUri();
        try
        {
            var recipes = await context.Recipes
                .AsNoTracking()
                .Where(recipe => recipe.Status == RecipeStatus.Published)
                .Select(recipe => new SitemapEntry(
                    $"/recipes/{recipe.Slug}",
                    recipe.UpdatedAt ?? recipe.CreatedAt,
                    "weekly",
                    0.8m))
                .ToListAsync(cancellationToken);

            var categories = await context.Categories
                .AsNoTracking()
                .Select(category => new SitemapEntry(
                    $"/categories/{category.Slug}",
                    category.UpdatedAt ?? category.CreatedAt,
                    "weekly",
                    0.6m))
                .ToListAsync(cancellationToken);

            var staticEntries = StaticRoutes.Select(route => new SitemapEntry(
                route.Path,
                DateTime.UtcNow,
                route.Frequency,
                route.Priority));
            var content = generator.Generate(recipes.Concat(categories).Concat(staticEntries));

            await storage.EnsureBucketExistsAsync(bucketName, cancellationToken);
            await using var stream = new MemoryStream(content, writable: false);
            await storage.UploadAsync(
                bucketName,
                "sitemap.xml",
                stream,
                content.Length,
                "application/xml",
                cancellationToken);

            if (publicSitemapUri is null)
            {
                logger.LogWarning(
                    "Google sitemap ping skipped because Sitemap:PublicSitemapUrl is not configured. The sitemap was stored, but search engines will not be notified.");
            }
            else
            {
                // TECH-RISK-013: the ping endpoint may be deprecated/unavailable; the ping
                // client logs failures at Info level and never throws, so a ping failure
                // cannot lose the already-stored sitemap.
                await pingClient.NotifyAsync(publicSitemapUri, cancellationToken);
            }

            logger.LogInformation(
                "Sitemap generated with {RecipeCount} published recipes and {CategoryCount} categories.",
                recipes.Count,
                categories.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Sitemap generation or storage failed.");
            throw;
        }
    }
}

using System.Diagnostics;
using System.Xml.Linq;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure;
using IdentityApplicationUser = CulinaryBlog.Infrastructure.Identity.ApplicationUser;
using CulinaryBlog.Infrastructure.Persistence;
using CulinaryBlog.Infrastructure.Storage;
using FluentAssertions;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Sitemap;

/// <summary>
/// FR-JOB-003 end-to-end scheduled-run proof (SRS-AUDIT sec-3.6 / NFR-SEO-003).
///
/// The REAL SitemapScheduleHostedService registers the recurring job
/// "daily-sitemap-generation" (cron "0 2 * * *", UTC) into a REAL PostgreSQL Hangfire
/// storage. The already-registered recurring job is then triggered through the Hangfire API
/// (NOT by calling SitemapJob directly), executed by a real BackgroundJobServer, and writes
/// sitemap.xml to an ISOLATED MinIO. The test reads that object back and asserts a valid XML
/// containing the static routes plus the Published recipe/category fixtures (and no Draft
/// recipe).
///
/// Environment gate: this test only runs when the ACTUAL isolated endpoints are provided AND
/// match the EXPECTED authority, so it can only ever touch the dedicated local PostgreSQL +
/// MinIO created for this run. No shared/production database, bucket or container is used.
/// When unconfigured it is Skipped (never Passed). Cleanup removes only this test's own
/// object / rows / recurring job; it never removes a container or volume.
/// </summary>
public sealed class SitemapScheduledRunTests : IAsyncLifetime
{
    private const string RecurringJobId = "daily-sitemap-generation";
    private const string Cron = "0 2 * * *";
    private const string CategorySlug = "sitemap-it-category";
    private const string RecipeSlug = "sitemap-it-soup";
    private const string DraftSlug = "sitemap-it-draft";
    private const string AuthorUserName = "sitemap-it-author";

    private IHost? host;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (host is null)
        {
            return;
        }

        try
        {
            await host.StopAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Shutdown is best-effort; the assertion result must not be masked.
        }

        host.Dispose();
    }

    [SitemapScheduleFact]
    public async Task ScheduledSitemap_RunsThroughHangfire_OnIsolatedPostgresAndMinio()
    {
        // Defensive re-validation: the Fact attribute already skips when unconfigured, but
        // fail closed here too so a mid-run env change never touches shared data.
        var env = SitemapScheduleTestEnvironment.ConfiguredOrThrow();
        var configuration = env.BuildConfiguration();

        var runHost = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddInfrastructure(configuration, environment: null))
            .Build();
        host = runHost;

        // Storage is only available once the host starts; cleanup tolerates it being null when
        // an earlier step failed before the scheduler registered anything.
        JobStorage? storage = null;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? testFailure = null;

        // Tracks whether THIS run actually published sitemap.xml. Cleanup only deletes the
        // object when this is true, so pre-existing data in the bucket is never removed.
        var sitemapPublishedByRun = false;

        try
        {
            // 1) Apply the app schema + seed fixtures on the isolated PostgreSQL BEFORE the
            //    scheduler starts, so the job has data to read.
            await MigrateAndSeedAsync(runHost.Services);

            // 2) Start the host the production / non-Testing way: MinIO initializer, real Hangfire
            //    PostgreSQL servers, and the REAL SitemapScheduleHostedService that registers the
            //    recurring job.
            await runHost.StartAsync();

            // 3) Verify the recurring job registration is persisted in the Hangfire storage.
            storage = runHost.Services.GetRequiredService<JobStorage>();
            AssertRecurringJobRegistered(storage);

            // 3a) Refuse to run when the bucket already holds sitemap.xml. This run must own the
            //     object it eventually deletes, so it never touches pre-existing data.
            await EnsureSitemapObjectAbsentAsync(runHost, env);

            // 4) Trigger the registered recurring job through the Hangfire API (no direct
            //    SitemapJob call) and wait until it Succeeds on the real worker.
            var jobId = new RecurringJobManager(storage).TriggerJob(RecurringJobId);
            jobId.Should().NotBeNullOrWhiteSpace(
                "TriggerJob must enqueue a background job for the registered recurring job");
            await WaitUntilSucceededAsync(storage, jobId!);

            // The job Succeeded and we already proved sitemap.xml was absent before the trigger,
            // so this run published the object. Mark it now - BEFORE reading/asserting the XML -
            // so cleanup can still delete the object if the download/assert step below fails.
            sitemapPublishedByRun = true;

            // 5) Read sitemap.xml back from the isolated MinIO and assert the XML.
            var xml = await DownloadSitemapAsync(runHost.Services, env.Bucket);
            AssertSitemap(xml);
        }
        catch (Exception exception)
        {
            // Capture so cleanup still runs in finally and the real failure is re-thrown below
            // (never silently swallowed).
            testFailure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            // 6) Cleanup ALWAYS runs (object / rows / recurring job of this run; never a
            //    container or volume). Errors are collected and surfaced, never swallowed.
            var cleanupErrors = await CollectCleanupErrorsAsync(
                runHost, storage, env, deleteSitemapObject: sitemapPublishedByRun);
            if (testFailure is not null)
            {
                throw cleanupErrors.Count > 0
                    ? new AggregateException(
                        "The scheduled run failed and cleanup also reported errors.",
                        new[] { testFailure.SourceException }.Concat(cleanupErrors))
                    : new AggregateException(testFailure.SourceException);
            }

            if (cleanupErrors.Count > 0)
            {
                throw new AggregateException(
                    "The scheduled run passed but cleanup failed and may have left test data behind.",
                    cleanupErrors);
            }
        }
    }

    private static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        // Make the seed idempotent so a previous partial run (that failed after seeding) does
        // not leave duplicate rows behind. Only this run's own fixtures are removed.
        await PurgeFixturesAsync(db);

        var userManager = provider.GetRequiredService<UserManager<IdentityApplicationUser>>();
        var author = new IdentityApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = AuthorUserName,
            Email = "sitemap-it-author@example.test",
            DisplayName = "Sitemap IT Author"
        };
        var created = await userManager.CreateAsync(author, "Test!2345Pass");
        created.Succeeded.Should().BeTrue(
            "a test author is required to satisfy Recipe.AuthorId FK: "
            + string.Join(";", created.Errors.Select(e => e.Description)));

        var category = new Category { Name = "Sitemap IT Category", Slug = CategorySlug };
        db.Categories.Add(category);

        db.Recipes.Add(new Recipe
        {
            Title = "Sitemap IT Soup",
            Slug = RecipeSlug,
            AuthorId = author.Id,
            CategoryId = category.Id,
            Status = RecipeStatus.Published
        });
        db.Recipes.Add(new Recipe
        {
            Title = "Sitemap IT Draft",
            Slug = DraftSlug,
            AuthorId = author.Id,
            CategoryId = category.Id,
            Status = RecipeStatus.Draft
        });

        await db.SaveChangesAsync();
    }

    private static async Task PurgeFixturesAsync(ApplicationDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
        {
            await connection.OpenAsync();
        }

        try
        {
            foreach (var slug in new[] { RecipeSlug, DraftSlug })
            {
                await DeleteAsync(connection, "DELETE FROM \"Recipes\" WHERE \"Slug\" = @s", slug);
            }

            await DeleteAsync(connection, "DELETE FROM \"Categories\" WHERE \"Slug\" = @s", CategorySlug);
            await DeleteAsync(connection, "DELETE FROM \"AspNetUsers\" WHERE \"UserName\" = @s", AuthorUserName);
        }
        finally
        {
            if (!wasOpen)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AssertRecurringJobRegistered(JobStorage storage)
    {
        using var connection = storage.GetConnection();

        var ids = connection.GetAllItemsFromSet("recurring-jobs");
        ids.Should().Contain(RecurringJobId,
            "the scheduler must persist the recurring job id into the Hangfire 'recurring-jobs' set");

        var hash = connection.GetAllEntriesFromHash($"recurring-job:{RecurringJobId}");
        hash.Should().ContainKey("Cron");
        hash["Cron"].Should().Be(Cron);
        hash.Should().ContainKey("TimeZoneId");
        hash["TimeZoneId"].Should().Be("UTC");
        hash.Should().ContainKey("Job");
        hash["Job"].Should().Contain("SitemapJob").And.Contain("GenerateAndPublishAsync");
    }

    private static async Task WaitUntilSucceededAsync(JobStorage storage, string jobId)
    {
        var monitoring = storage.GetMonitoringApi();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(120))
        {
            var latest = monitoring.JobDetails(jobId)?.History.FirstOrDefault();
            if (latest is not null)
            {
                if (latest.StateName == "Succeeded")
                {
                    return;
                }

                if (latest.StateName == "Failed")
                {
                    var message = latest.Data is not null &&
                        latest.Data.TryGetValue("ExceptionMessage", out var raw) ? raw : "(no message)";
                    throw new TimeoutException($"Sitemap job {jobId} entered Failed: {message}");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        throw new TimeoutException($"Sitemap job {jobId} did not reach Succeeded within 120 seconds.");
    }

    private static async Task<XDocument> DownloadSitemapAsync(IServiceProvider services, string bucket)
    {
        using var scope = services.CreateScope();
        var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorageClient>();
        await using var stream = await objectStorage.DownloadAsync(bucket, "sitemap.xml", CancellationToken.None);
        return XDocument.Load(stream);
    }

    private static void AssertSitemap(XDocument xml)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        xml.Root.Should().NotBeNull();
        xml.Root!.Name.LocalName.Should().Be("urlset");

        var locations = xml.Descendants(ns + "loc").Select(node => node.Value).ToList();
        const string baseUrl = "https://sitemap-it.example.test";
        locations.Should().Contain($"{baseUrl}/");
        locations.Should().Contain($"{baseUrl}/recipes");
        locations.Should().Contain($"{baseUrl}/categories");
        locations.Should().Contain($"{baseUrl}/search");
        locations.Should().Contain($"{baseUrl}/recipes/{RecipeSlug}");
        locations.Should().Contain($"{baseUrl}/categories/{CategorySlug}");
        locations.Should().NotContain(location => location.Contains(DraftSlug, StringComparison.Ordinal));
    }

    /// <summary>
    /// Removes only this run's own artifacts from the isolated services. Never touches a
    /// container or volume. The <c>sitemap.xml</c> object is deleted ONLY when
    /// <paramref name="deleteSitemapObject"/> is true (i.e. this run produced it), so pre-existing
    /// bucket data is never removed. Any failure is returned (not swallowed) so the caller can
    /// surface it as a real test error.
    /// </summary>
    private static async Task<List<Exception>> CollectCleanupErrorsAsync(
        IHost runHost,
        JobStorage? storage,
        SitemapScheduleTestEnvironment env,
        bool deleteSitemapObject)
    {
        var errors = new List<Exception>();

        if (deleteSitemapObject)
        {
            try
            {
                using var scope = runHost.Services.CreateScope();
                var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorageClient>();
                await objectStorage.DeleteAsync(env.Bucket, "sitemap.xml", CancellationToken.None);
            }
            catch (Exception exception)
            {
                errors.Add(new InvalidOperationException(
                    $"Cleanup failed to delete 'sitemap.xml' from isolated bucket '{env.Bucket}'.", exception));
            }
        }

        if (storage is not null)
        {
            try
            {
                new RecurringJobManager(storage).RemoveIfExists(RecurringJobId);
            }
            catch (Exception exception)
            {
                errors.Add(new InvalidOperationException(
                    $"Cleanup failed to remove recurring job '{RecurringJobId}' from Hangfire storage.", exception));
            }
        }

        try
        {
            await using var connection = new NpgsqlConnection(env.PostgresConnectionString);
            await connection.OpenAsync();
            await DeleteAsync(connection, "DELETE FROM \"Recipes\" WHERE \"Slug\" = @s", RecipeSlug);
            await DeleteAsync(connection, "DELETE FROM \"Recipes\" WHERE \"Slug\" = @s", DraftSlug);
            await DeleteAsync(connection, "DELETE FROM \"Categories\" WHERE \"Slug\" = @s", CategorySlug);
            await DeleteAsync(connection, "DELETE FROM \"AspNetUsers\" WHERE \"UserName\" = @s", AuthorUserName);
        }
        catch (Exception exception)
        {
            errors.Add(new InvalidOperationException(
                "Cleanup failed to delete seeded rows from the isolated PostgreSQL database.", exception));
        }

        return errors;
    }

    /// <summary>
    /// Fails fast when <c>sitemap.xml</c> already exists in the isolated bucket. This guarantees
    /// the object the test later deletes was produced by this run, never pre-existing data.
    /// </summary>
    private static async Task EnsureSitemapObjectAbsentAsync(IHost runHost, SitemapScheduleTestEnvironment env)
    {
        using var scope = runHost.Services.CreateScope();
        var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorageClient>();

        var exists = false;
        try
        {
            await using var stream = await objectStorage.DownloadAsync(env.Bucket, "sitemap.xml", CancellationToken.None);
            exists = stream is not null;
        }
        catch (Exception exception) when (IsObjectMissing(exception))
        {
            exists = false;
        }

        exists.Should().BeFalse(
            $"isolated bucket '{env.Bucket}' already contains 'sitemap.xml'; use a bucket unique to "
            + "this run so cleanup never removes pre-existing data.");
    }

    /// <summary>
    /// Heuristically detects a "object does not exist" error from the MinIO SDK / storage layer.
    /// Anything else (network, auth) is treated as NOT missing so the test surfaces the real error.
    /// </summary>
    private static bool IsObjectMissing(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName ?? string.Empty;
            if (typeName.Contains("ObjectNotFound", StringComparison.Ordinal) ||
                typeName.Contains("NoSuchKey", StringComparison.Ordinal) ||
                typeName.Contains("KeyNotFound", StringComparison.Ordinal))
            {
                return true;
            }

            if (current.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("NoSuchKey", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task DeleteAsync(System.Data.Common.DbConnection connection, string sql, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "s";
        parameter.Value = value;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// Reads the isolated-run endpoints strictly from environment variables, normalizes the MinIO
/// endpoint to a host:port authority (keeping the port), and refuses to run unless every value
/// is present and the actual authority exactly matches the expected one. No fallback bucket.
/// </summary>
internal sealed class SitemapScheduleTestEnvironment
{
    public const string PostgresVariable = "SITEMAP_IT_POSTGRES";
    public const string MinioEndpointVariable = "SITEMAP_IT_MINIO_ENDPOINT";
    public const string MinioAccessKeyVariable = "SITEMAP_IT_MINIO_ACCESS_KEY";
    public const string MinioSecretKeyVariable = "SITEMAP_IT_MINIO_SECRET_KEY";
    public const string MinioBucketVariable = "SITEMAP_IT_MINIO_BUCKET";
    public const string ExpectedPostgresVariable = "SITEMAP_IT_EXPECTED_PG_AUTHORITY";
    public const string ExpectedMinioVariable = "SITEMAP_IT_EXPECTED_MINIO_AUTHORITY";
    public const string ExpectedPostgresDatabaseVariable = "SITEMAP_IT_EXPECTED_PG_DATABASE";
    public const string ExpectedBucketVariable = "SITEMAP_IT_EXPECTED_BUCKET";

    /// <summary>Database names that belong to the shared/production environment and must never be used here.</summary>
    private static readonly HashSet<string> SharedDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "culinary_blog",
        "culinary_blog_e2e",
        "postgres",
        "minio_it"
    };

    /// <summary>Bucket names that belong to the shared/production environment and must never be used here.</summary>
    private static readonly HashSet<string> SharedBucketNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "culinary-blog",
        "culinary-blog-it"
    };

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string MinioEndpoint { get; private set; } = string.Empty;
    public string MinioAccessKey { get; private set; } = string.Empty;
    public string MinioSecretKey { get; private set; } = string.Empty;
    public string Bucket { get; private set; } = string.Empty;

    public IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = PostgresConnectionString,
            ["Jwt:Key"] = "SitemapItOnlySigningKey_NotARealCredential_0123456789",
            ["Redis:ConnectionString"] = string.Empty,
            ["MinIO:Endpoint"] = MinioEndpoint,
            ["MinIO:AccessKey"] = MinioAccessKey,
            ["MinIO:SecretKey"] = MinioSecretKey,
            ["MinIO:UseSSL"] = "false",
            // Explicit isolated bucket: never the shared "culinary-blog".
            ["MinIO:BucketName"] = Bucket,
            ["MinIO:PublicBaseUrl"] = $"http://{MinioEndpoint}/{Bucket}",
            ["Sitemap:PublicBaseUrl"] = "https://sitemap-it.example.test",
            // Empty on purpose: no Google ping is sent from this test.
            ["Sitemap:PublicSitemapUrl"] = string.Empty
        })
        .Build();

    public static SitemapScheduleTestEnvironment ConfiguredOrThrow()
    {
        var reason = ComputeSkipReason();
        if (reason is not null)
        {
            throw new InvalidOperationException(
                "FR-JOB-003 isolated scheduled-run test cannot run: " + reason);
        }

        return new SitemapScheduleTestEnvironment
        {
            PostgresConnectionString = Read(PostgresVariable)!,
            MinioEndpoint = NormalizeAuthority(Read(MinioEndpointVariable)!),
            MinioAccessKey = Read(MinioAccessKeyVariable)!,
            MinioSecretKey = Read(MinioSecretKeyVariable)!,
            Bucket = Read(MinioBucketVariable)!
        };
    }

    public static string? SkipReason() => ComputeSkipReason();

    private static string? ComputeSkipReason()
    {
        var postgres = Read(PostgresVariable);
        var minioEndpoint = Read(MinioEndpointVariable);
        var accessKey = Read(MinioAccessKeyVariable);
        var secretKey = Read(MinioSecretKeyVariable);
        var bucket = Read(MinioBucketVariable);

        if (postgres is null || minioEndpoint is null || accessKey is null || secretKey is null)
        {
            return $"set {PostgresVariable}, {MinioEndpointVariable}, {MinioAccessKeyVariable} and "
                + $"{MinioSecretKeyVariable} to the isolated run's PostgreSQL and MinIO.";
        }

        // Bucket must be explicit: no fallback to culinary-blog-it or culinary-blog.
        if (bucket is null)
        {
            return $"{MinioBucketVariable} is required and must be an explicit isolated bucket "
                + "(no fallback to the shared culinary-blog bucket).";
        }

        // The bucket must be the dedicated isolated bucket for this run, never a shared/production
        // bucket. It must match the expected bucket name exactly and must not be a shared name.
        var expectedBucket = Read(ExpectedBucketVariable);
        if (expectedBucket is null)
        {
            return $"{ExpectedBucketVariable} must be set to the dedicated isolated bucket name "
                + "for this run.";
        }

        if (!string.Equals(bucket, expectedBucket, StringComparison.Ordinal))
        {
            return $"MinIO bucket '{bucket}' does not match the expected isolated bucket "
                + $"'{expectedBucket}'.";
        }

        if (SharedBucketNames.Contains(bucket))
        {
            return $"MinIO bucket '{bucket}' is a shared/production bucket and must not be used "
                + "for this isolated run.";
        }

        // MinIO authority must exactly match the expected isolated endpoint.
        var minioAuthority = NormalizeAuthority(minioEndpoint);
        var expectedMinio = Read(ExpectedMinioVariable);
        if (expectedMinio is null)
        {
            return $"{ExpectedMinioVariable} must be set to the expected isolated MinIO "
                + "authority (host:port) for this run.";
        }

        if (!string.Equals(minioAuthority, NormalizeAuthority(expectedMinio), StringComparison.OrdinalIgnoreCase))
        {
            return $"MinIO endpoint '{minioAuthority}' does not match the expected isolated "
                + $"authority '{expectedMinio}'.";
        }

        // The MinIO endpoint must itself be a loopback host:port (never a remote/shared host),
        // and the authority must be a well-formed host:port with a valid TCP port.
        if (!TryParseLoopbackAuthority(minioAuthority, out _, out _))
        {
            return $"MinIO endpoint '{minioAuthority}' must be a loopback host:port "
                + "(for example 127.0.0.1:19000) for this isolated run.";
        }

        // PostgreSQL authority must exactly match the expected isolated endpoint and be loopback.
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = new NpgsqlConnectionStringBuilder(postgres);
        }
        catch (Exception exception)
        {
            return $"{PostgresVariable} is not a valid Npgsql connection string: {exception.Message}";
        }

        var postgresAuthority = $"{builder.Host}:{builder.Port}";
        var expectedPostgres = Read(ExpectedPostgresVariable);
        if (expectedPostgres is null)
        {
            return $"{ExpectedPostgresVariable} must be set to the expected isolated PostgreSQL "
                + "authority (host:port) for this run.";
        }

        if (!string.Equals(postgresAuthority, NormalizeAuthority(expectedPostgres), StringComparison.OrdinalIgnoreCase))
        {
            return $"PostgreSQL authority '{postgresAuthority}' does not match the expected "
                + $"isolated authority '{expectedPostgres}'.";
        }

        if (string.IsNullOrWhiteSpace(builder.Host) || !IsLoopback(builder.Host!))
        {
            return $"PostgreSQL host must be local loopback for this isolated run (got '{builder.Host}').";
        }

        // The PostgreSQL database name must be the dedicated isolated database, never a shared or
        // production database name.
        var expectedDatabase = Read(ExpectedPostgresDatabaseVariable);
        if (expectedDatabase is null)
        {
            return $"{ExpectedPostgresDatabaseVariable} must be set to the dedicated isolated "
                + "database name for this run.";
        }

        if (!string.Equals(builder.Database, expectedDatabase, StringComparison.Ordinal))
        {
            return $"PostgreSQL database '{builder.Database}' does not match the expected isolated "
                + $"database '{expectedDatabase}'.";
        }

        if (SharedDatabaseNames.Contains(expectedDatabase))
        {
            return $"PostgreSQL database '{expectedDatabase}' is a shared/production database name "
                + "and must not be used for this isolated run.";
        }

        return null;
    }

    /// <summary>
    /// Validates that an authority is a well-formed loopback host:port (host is loopback and the
    /// port is a valid TCP port). The host part keeps any IPv6 brackets; the port must parse to a
    /// value in 1..65535.
    /// </summary>
    private static bool TryParseLoopbackAuthority(string authority, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var separator = authority.LastIndexOf(':');
        if (separator <= 0 || separator == authority.Length - 1)
        {
            return false;
        }

        host = authority[..separator];
        var portText = authority[(separator + 1)..];
        if (!int.TryParse(portText, out port) || port < 1 || port > 65535)
        {
            return false;
        }

        return IsLoopback(host.Trim('[', ']'));
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host == "::1";

    private static string NormalizeAuthority(string value)
    {
        var normalized = value.Trim().TrimEnd('/');
        foreach (var prefix in new[] { "https://", "http://" })
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..];
            }
        }

        return normalized.TrimEnd('/');
    }

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// Fact that is Skipped (never Passed) unless the isolated PostgreSQL + MinIO endpoints are
/// provided and match the expected authorities.
/// </summary>
public sealed class SitemapScheduleFactAttribute : FactAttribute
{
    public SitemapScheduleFactAttribute()
    {
        var reason = SitemapScheduleTestEnvironment.SkipReason();
        if (reason is not null)
        {
            Skip = reason;
        }
    }
}

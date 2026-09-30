using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Storage;

/// <summary>
/// Ensures the configured MinIO bucket exists and exposes a public-read policy
/// limited to <c>s3:GetObject</c> (NFR-SEC-004) whenever the application starts.
/// Connectivity problems are logged as warnings so the application still boots.
/// </summary>
public sealed class StorageStartupInitializer : IHostedService
{
    private readonly IObjectStorageClient client;
    private readonly ILogger<StorageStartupInitializer> logger;
    private readonly string bucketName;

    public StorageStartupInitializer(
        IObjectStorageClient client,
        ILogger<StorageStartupInitializer> logger,
        string bucketName)
    {
        this.client = client;
        this.logger = logger;
        this.bucketName = bucketName?.Trim() ?? string.Empty;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.EnsureBucketExistsAsync(bucketName, cancellationToken);
            await client.EnsureBucketPolicyAsync(
                bucketName,
                BuildPublicReadPolicy(bucketName),
                cancellationToken);

            logger.LogInformation(
                "MinIO bucket '{Bucket}' is ready with public-read policy limited to s3:GetObject.",
                bucketName);
        }
        catch (Exception ex)
        {
            // MinIO may not be reachable yet in local development; never block application startup.
            logger.LogWarning(
                ex,
                "MinIO storage initialization skipped for bucket '{Bucket}'. " +
                "Bucket/object uploads will be retried on demand.",
                bucketName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string BuildPublicReadPolicy(string bucketName)
        => $$"""
        {
          "Version": "2012-10-17",
          "Statement": [
            {
              "Sid": "PublicReadGetObjectOnly",
              "Effect": "Allow",
              "Principal": "*",
              "Action": ["s3:GetObject"],
              "Resource": ["arn:aws:s3:::{{bucketName}}/*"]
            }
          ]
        }
        """;
}

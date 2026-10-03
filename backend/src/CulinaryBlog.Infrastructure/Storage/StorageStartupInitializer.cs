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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown must not be reported as a MinIO connectivity problem.
            throw;
        }
        catch (Exception ex)
        {
            // MinIO may not be reachable yet in local development; never block application startup.
            // Only the bucket check is retried on demand by the storage service; the public-read
            // policy is applied on the next successful startup.
            logger.LogWarning(
                ex,
                "MinIO storage initialization skipped for bucket '{Bucket}'. " +
                "The bucket is ensured on demand, but the public-read policy " +
                "requires a later successful startup.",
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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Health;

/// <summary>
/// MinIO probe for the aggregate health endpoint. MinIO does not affect
/// readiness (FR-OBS-001), so this probe only feeds <c>/health</c>.
///
/// <see cref="IMinioClient"/> is registered only when MinIO is configured, so a
/// missing client is exactly the <c>UnavailableFileStorageService</c> case.
/// The probe resolves the client directly instead of
/// <c>IFileStorageService</c>, so it observes storage availability rather than
/// the storage service's own availability policy, and it checks for the bucket
/// without creating it (a missing bucket is unhealthy, not repaired).
/// </summary>
public sealed class MinioProbe : IMinioProbe
{
    /// <summary>
    /// Must stay identical to the fallback in
    /// <c>CulinaryBlog.Infrastructure.DependencyInjection.AddInfrastructure</c>,
    /// otherwise the probe would report on a different bucket than the one the
    /// application actually uses.
    /// </summary>
    private const string DefaultBucketName = "culinary-blog";

    private readonly IServiceProvider services;
    private readonly IConfiguration configuration;

    public MinioProbe(IServiceProvider services, IConfiguration configuration)
    {
        this.services = services;
        this.configuration = configuration;
    }

    public async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var client = services.GetService<IMinioClient>();
        if (client is null)
        {
            return false;
        }

        var bucketName = configuration["MinIO:BucketName"] ?? DefaultBucketName;

        return await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName),
            ct);
    }
}
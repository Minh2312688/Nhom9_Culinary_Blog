using System.Text.Json;
using CulinaryBlog.Application.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure.Persistence;

/// <summary>
/// Category cache trên Redis distributed cache (IDistributedCache).
/// Invalidate dùng cache versioning: mỗi lần invalidate sẽ đổi "generation", khiến mọi key
/// đã ghi trước đó không còn được đọc nữa. Cách này không cần API scan/prefix (không có trong
/// IDistributedCache) và vẫn đúng khi chạy với provider distributed khác.
/// Khi Redis lỗi: log warning và trả miss để handler đọc lại database — không trả success giả.
/// </summary>
public sealed class DistributedCategoryCache : ICategoryCache
{
    private const string GenerationKey = "categories:generation";
    private const string InitialGeneration = "0";
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromHours(24);

    private readonly IDistributedCache cache;
    private readonly ILogger<DistributedCategoryCache> logger;

    public DistributedCategoryCache(IDistributedCache cache, ILogger<DistributedCategoryCache> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await cache.GetStringAsync(await BuildKeyAsync(key, cancellationToken), cancellationToken);
            return value is null ? default : JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            logger.LogWarning(
                exception,
                "Category cache read failed for {CacheKey}; falling back to database.",
                key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.SetStringAsync(
                await BuildKeyAsync(key, cancellationToken),
                JsonSerializer.Serialize(value),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            logger.LogWarning(exception, "Category cache write failed for {CacheKey}.", key);
        }
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.SetStringAsync(
                GenerationKey,
                Guid.NewGuid().ToString("N"),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = GenerationTtl },
                cancellationToken);
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            logger.LogWarning(exception, "Category cache invalidation failed.");
        }
    }

    private async Task<string> BuildKeyAsync(string key, CancellationToken cancellationToken)
    {
        var generation = await cache.GetStringAsync(GenerationKey, cancellationToken);
        return $"{key}:g{generation ?? InitialGeneration}";
    }

    private static bool IsCacheFailure(Exception exception) =>
        exception is RedisConnectionException or TimeoutException;
}
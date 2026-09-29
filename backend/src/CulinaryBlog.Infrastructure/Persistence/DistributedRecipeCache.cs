using System.Text.Json;
using CulinaryBlog.Application.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class DistributedRecipeCache : IRecipeCache
{
    private readonly IDistributedCache cache;
    private readonly ILogger<DistributedRecipeCache> logger;

    public DistributedRecipeCache(IDistributedCache cache, ILogger<DistributedRecipeCache> logger)
    {
        this.cache = cache;
        this.logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await cache.GetStringAsync(key, cancellationToken);
            return value is null ? default : JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception exception) when (exception is RedisConnectionException or TimeoutException)
        {
            logger.LogWarning(exception, "Cache read failed for {CacheKey}; continuing with database.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is RedisConnectionException or TimeoutException)
        {
            logger.LogWarning(exception, "Cache write failed for {CacheKey}; continuing without cache.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception exception) when (exception is RedisConnectionException or TimeoutException)
        {
            logger.LogWarning(exception, "Cache removal failed for {CacheKey}; continuing.", key);
        }
    }

    // IDistributedCache has no portable key scan. List queries are deliberately not cached,
    // so invalidation only needs to remove known detail keys at this stage.
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

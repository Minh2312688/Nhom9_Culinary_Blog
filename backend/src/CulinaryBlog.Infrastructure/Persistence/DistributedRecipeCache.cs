using System.Text.Json;
using CulinaryBlog.Application.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CulinaryBlog.Infrastructure.Persistence;

public sealed class DistributedRecipeCache : IRecipeCache
{
    private const string GenerationKey = "recipes:generation";
    private const string InitialGeneration = "0";
    private static readonly TimeSpan GenerationTtl = TimeSpan.FromHours(24);

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
            var resolvedKey = await ResolveKeyAsync(key, cancellationToken);
            var value = await cache.GetStringAsync(resolvedKey, cancellationToken);
            return value is null ? default : JsonSerializer.Deserialize<T>(value);
        }
        catch (Exception exception) when (exception is RedisConnectionException or TimeoutException or JsonException)
        {
            logger.LogWarning(exception, "Cache read failed for {CacheKey}; continuing with database.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedKey = await ResolveKeyAsync(key, cancellationToken);
            await cache.SetStringAsync(resolvedKey, JsonSerializer.Serialize(value), new DistributedCacheEntryOptions
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
            var normalized = NormalizeKey(key);
            await cache.RemoveAsync(normalized, cancellationToken);
        }
        catch (Exception exception) when (exception is RedisConnectionException or TimeoutException)
        {
            logger.LogWarning(exception, "Cache removal failed for {CacheKey}; continuing.", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var newGeneration = Guid.NewGuid().ToString("N");
            await cache.SetStringAsync(
                GenerationKey,
                newGeneration,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = GenerationTtl },
                cancellationToken);
            logger.LogInformation("Recipe cache prefix {Prefix} invalidated with new generation {Generation}.", prefix, newGeneration);
        }
        catch (Exception exception) when (exception is RedisConnectionException or TimeoutException)
        {
            logger.LogWarning(exception, "Recipe cache prefix removal failed for {Prefix}; continuing.", prefix);
        }
    }

    private async Task<string> ResolveKeyAsync(string key, CancellationToken cancellationToken)
    {
        var normalized = NormalizeKey(key);
        // Prefix-invalidated search result keys incorporate generation
        if (normalized.StartsWith("recipes:search:", StringComparison.OrdinalIgnoreCase))
        {
            var generation = await cache.GetStringAsync(GenerationKey, cancellationToken);
            return $"{normalized}:g{generation ?? InitialGeneration}";
        }

        return normalized;
    }

    private static string NormalizeKey(string key) => key.Trim().ToLowerInvariant();
}

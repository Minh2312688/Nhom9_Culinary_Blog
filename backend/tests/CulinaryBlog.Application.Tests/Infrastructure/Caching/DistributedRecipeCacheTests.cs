using CulinaryBlog.Infrastructure.Persistence;
using StackExchange.Redis;
using System.Text;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Caching;

public sealed class DistributedRecipeCacheTests
{
    [Fact]
    public async Task GetAsync_InvalidJson_ReturnsCacheMissAndLogsWarning()
    {
        var distributedCache = new InMemoryDistributedCache();
        distributedCache.Set("recipes:test", Encoding.UTF8.GetBytes("not-json"), new());
        var logger = new RecordingLogger<DistributedRecipeCache>();
        var cache = new DistributedRecipeCache(distributedCache, logger);

        var result = await cache.GetAsync<object>("recipes:test");

        Assert.Null(result);
        Assert.NotEmpty(logger.Warnings);
    }

    [Fact]
    public async Task GetAsync_RedisConnectionFailure_ReturnsCacheMissAndLogsWarning()
    {
        var logger = new RecordingLogger<DistributedRecipeCache>();
        var cache = new DistributedRecipeCache(
            new ThrowingDistributedCache(new RedisConnectionException(
                ConnectionFailureType.UnableToResolvePhysicalConnection, "Redis is unavailable")),
            logger);

        var result = await cache.GetAsync<object>("recipes:test");

        Assert.Null(result);
        Assert.NotEmpty(logger.Warnings);
    }
}

using CulinaryBlog.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure;

public sealed class DistributedRecipeCacheTests
{
    [Fact]
    public async Task RemoveByPrefix_InvalidatesRecipeListEntries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        using var provider = services.BuildServiceProvider();
        var cache = new DistributedRecipeCache(
            provider.GetRequiredService<IDistributedCache>(),
            provider.GetRequiredService<ILogger<DistributedRecipeCache>>());

        await cache.SetAsync("recipes:list:public", "cached", TimeSpan.FromMinutes(1));
        Assert.Equal("cached", await cache.GetAsync<string>("recipes:list:public"));

        await cache.RemoveByPrefixAsync("recipes:");

        Assert.Null(await cache.GetAsync<string>("recipes:list:public"));
    }
}

using CulinaryBlog.Application.Common.Models;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Caching;

/// <summary>
/// Unit test cho DistributedCategoryCache: serialize/deserialize, TTL truyền vào
/// DistributedCacheEntryOptions, cache versioning (invalidate) và fallback về database
/// khi Redis lỗi (log warning + không ném exception ra ngoài).
/// </summary>
public class DistributedCategoryCacheTests
{
    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnStoredValue()
    {
        // Arrange
        var store = new InMemoryDistributedCache();
        var cache = CreateCache(store);

        // Act
        await cache.SetAsync("categories:slug:banh-ngot", "Bánh Ngọt", TimeSpan.FromMinutes(30));
        var value = await cache.GetAsync<string>("categories:slug:banh-ngot");

        // Assert
        value.Should().Be("Bánh Ngọt");
    }

    [Fact]
    public async Task SetAsync_ShouldApplyRequestedTtl()
    {
        // Arrange
        var store = new InMemoryDistributedCache();
        var cache = CreateCache(store);

        // Act
        await cache.SetAsync("categories:list:p=1", "value", TimeSpan.FromMinutes(30));

        // Assert
        store.SetCalls.Should().ContainSingle();
        store.SetCalls[0].Ttl.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task GetAsync_MissingKey_ShouldReturnDefault()
    {
        // Arrange
        var cache = CreateCache(new InMemoryDistributedCache());

        // Act
        var value = await cache.GetAsync<string>("categories:slug:khong-ton-tai");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateAsync_ShouldMakePreviouslyCachedKeysUnreachable()
    {
        // Arrange: cache versioning đảm bảo không cần SCAN/prefix delete
        var store = new InMemoryDistributedCache();
        var cache = CreateCache(store);
        await cache.SetAsync("categories:list:p=1", "value", TimeSpan.FromMinutes(30));

        // Act
        await cache.InvalidateAsync();

        // Assert
        (await cache.GetAsync<string>("categories:list:p=1")).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_WhenRedisThrows_ShouldLogWarningAndReturnNullForDatabaseFallback()
    {
        // Arrange
        var logger = new RecordingLogger<DistributedCategoryCache>();
        var cache = new DistributedCategoryCache(
            new ThrowingDistributedCache(
                new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down")),
            logger);

        // Act
        var value = await cache.GetAsync<string>("categories:list:p=1");

        // Assert: đọc lại database, log rõ ràng, không ném exception ra ngoài
        value.Should().BeNull();
        logger.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task SetAsync_WhenRedisThrows_ShouldLogWarningAndSwallow()
    {
        // Arrange
        var logger = new RecordingLogger<DistributedCategoryCache>();
        var cache = new DistributedCategoryCache(
            new ThrowingDistributedCache(new TimeoutException("redis timeout")),
            logger);

        // Act
        await cache.SetAsync("categories:list:p=1", "value", TimeSpan.FromMinutes(30));

        // Assert
        logger.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task InvalidateAsync_WhenRedisThrows_ShouldLogWarningAndSwallow()
    {
        // Arrange
        var logger = new RecordingLogger<DistributedCategoryCache>();
        var cache = new DistributedCategoryCache(
            new ThrowingDistributedCache(
                new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis down")),
            logger);

        // Act
        await cache.InvalidateAsync();

        // Assert
        logger.Warnings.Should().ContainSingle();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_PaginatedResult_ShouldRoundTrip()
    {
        // Arrange: chứng minh payload list thực sự đọc lại được từ cache
        var store = new InMemoryDistributedCache();
        var cache = CreateCache(store);
        var expected = new PaginatedResult<CategoryDto>(
            [new CategoryDto(Guid.NewGuid(), "Bánh Ngọt", "banh-ngot", "Mô tả", null, 3, DateTime.UtcNow)],
            1,
            1,
            10);

        // Act
        await cache.SetAsync("categories:list:p=1", expected, TimeSpan.FromMinutes(30));
        var actual = await cache.GetAsync<PaginatedResult<CategoryDto>>("categories:list:p=1");

        // Assert
        actual.Should().NotBeNull();
        actual!.Items.Should().ContainSingle();
        actual.Items[0].Name.Should().Be("Bánh Ngọt");
        actual.Items[0].OrderIndex.Should().Be(3);
        actual.TotalCount.Should().Be(1);
        actual.TotalPages.Should().Be(1);
        actual.HasNextPage.Should().BeFalse();
    }

    private static DistributedCategoryCache CreateCache(IDistributedCache store) =>
        new(store, new RecordingLogger<DistributedCategoryCache>());
}
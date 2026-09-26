using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Tests.Infrastructure.Caching;

/// <summary>Test doubles cho unit test cache, không cần Redis thật.</summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel == LogLevel.Warning)
        {
            Warnings.Add(formatter(state, exception));
        }
    }
}

internal sealed class InMemoryDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> store = new(StringComparer.Ordinal);

    public List<(string Key, TimeSpan? Ttl)> SetCalls { get; } = new();

    public byte[]? Get(string key) => store.TryGetValue(key, out var value) ? value : null;

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Task.FromResult(Get(key));

    public void Refresh(string key)
    {
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public void Remove(string key) => store.Remove(key);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        Remove(key);
        return Task.CompletedTask;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        store[key] = value;
        SetCalls.Add((key, options.AbsoluteExpirationRelativeToNow));
    }

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingDistributedCache(Exception exception) : IDistributedCache
{
    public byte[]? Get(string key) => throw exception;

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw exception;

    public void Refresh(string key) => throw exception;

    public Task RefreshAsync(string key, CancellationToken token = default) => throw exception;

    public void Remove(string key) => throw exception;

    public Task RemoveAsync(string key, CancellationToken token = default) => throw exception;

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw exception;

    public Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default) => throw exception;
}
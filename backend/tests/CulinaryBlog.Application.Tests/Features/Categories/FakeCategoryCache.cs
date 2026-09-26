using CulinaryBlog.Application.Contracts;

namespace CulinaryBlog.Application.Tests.Features.Categories;

/// <summary>
/// Test double cho ICategoryCache: lưu entry trong bộ nhớ, ghi lại lệnh Set/Invalidate
/// để assert cache hit/miss/invalidation mà không cần Redis thật.
/// </summary>
internal sealed class FakeCategoryCache : ICategoryCache
{
    private readonly Dictionary<string, object?> entries = new(StringComparer.Ordinal);

    public List<(string Key, TimeSpan Ttl)> SetCalls { get; } = new();

    public int InvalidateCount { get; private set; }

    public int GetCount { get; private set; }

    public void Seed<T>(string key, T value) => entries[key] = value;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        GetCount++;
        return Task.FromResult(entries.TryGetValue(key, out var value) ? (T?)value : default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        entries[key] = value;
        SetCalls.Add((key, ttl));
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        InvalidateCount++;
        entries.Clear();
        return Task.CompletedTask;
    }
}
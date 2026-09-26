public interface ICategoryCache
{
    /// <summary>Đọc cache theo logical key. Trả null khi miss hoặc khi Redis lỗi.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Ghi cache với TTL do caller quy định (contract Category: 30 phút).</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vô hiệu hoá toàn bộ cache Category (list + detail) sau Create/Update/Delete.
    /// Implement dùng cache versioning nên không cần API scan/prefix của IDistributedCache.
    /// </summary>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
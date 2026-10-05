using CulinaryBlog.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Tests.Infrastructure.Storage;

/// <summary>
/// Test double cho <see cref="StorageStartupInitializer"/>: ghi lại thứ tự gọi
/// để kiểm chứng bucket được ensure trước khi áp policy, và cho phép giả lập MinIO lỗi.
/// </summary>
internal sealed class RecordingObjectStorageClient : IObjectStorageClient
{
    private readonly Exception? bucketFailure;
    private readonly Exception? policyFailure;

    public RecordingObjectStorageClient(Exception? bucketFailure = null, Exception? policyFailure = null)
    {
        this.bucketFailure = bucketFailure;
        this.policyFailure = policyFailure;
    }

    /// <summary>Thứ tự lời gọi thực tế, dạng "Method:bucket".</summary>
    public List<string> CallLog { get; } = new();

    public List<(string Bucket, string PolicyJson)> PolicyCalls { get; } = new();

    public Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallLog.Add($"EnsureBucketExists:{bucketName}");

        return bucketFailure is null ? Task.CompletedTask : Task.FromException(bucketFailure);
    }

    public Task EnsureBucketPolicyAsync(
        string bucketName,
        string policyJson,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallLog.Add($"EnsureBucketPolicy:{bucketName}");
        PolicyCalls.Add((bucketName, policyJson));

        return policyFailure is null ? Task.CompletedTask : Task.FromException(policyFailure);
    }

    public Task UploadAsync(
        string bucketName,
        string objectName,
        Stream content,
        long sizeBytes,
        string contentType,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Startup tests must not upload objects.");

    public Task DeleteAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Startup tests must not delete objects.");
}

/// <summary>Test double ghi lại mọi log entry để kiểm chứng hành vi non-fatal và cancellation.</summary>
internal sealed class RecordingStorageLogger : ILogger<StorageStartupInitializer>
{
    public List<LogEntry> Entries { get; } = new();

    public IEnumerable<LogEntry> Warnings => Entries.Where(entry => entry.Level == LogLevel.Warning);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

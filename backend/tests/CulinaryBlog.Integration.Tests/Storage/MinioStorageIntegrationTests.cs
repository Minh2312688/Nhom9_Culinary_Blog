using System.Net;
using System.Net.Http.Headers;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Infrastructure;
using CulinaryBlog.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Storage;

/// <summary>
/// Integration test với MinIO thật (FR-FILE-001/FR-FILE-002, NFR-SEC-004).
///
/// Unit test với fake chỉ chứng minh JSON policy được sinh đúng và thứ tự gọi của
/// initializer; chúng KHÔNG chứng minh MinIO thực thi policy. Bộ test này mới xác minh
/// enforcement thật (anonymous GET 200 / PUT 403 / DELETE 403) và xóa idempotent.
///
/// Quy ước mã trạng thái (đã xác minh trực tiếp trên MinIO RELEASE.2025-09-07): khi bucket
/// policy `s3:GetObject`-only (least privilege, NFR-SEC-004) ĐÃ được áp, anonymous GET object
/// không tồn tại trả **404 NoSuchKey** — request được policy cho phép ở tầng bucket nên mới đi
/// tới bước tra object. Trái lại **403** nghĩa là policy CHƯA được áp (mọi anonymous GET bị chặn
/// ngay từ đầu) — chính là triệu chứng của lần chạy thử đầu tiên; vì vậy 404 được dùng làm bằng
/// chứng policy đã hiệu lực, không đơn thuần là "object không có".
///
/// Chạy bằng cách export MINIO_TEST_ENDPOINT, MINIO_TEST_ACCESS_KEY, MINIO_TEST_SECRET_KEY
/// (tùy chọn MINIO_TEST_PUBLIC_BASE_URL, MINIO_TEST_BUCKET) trước khi gọi dotnet test.
/// Thiếu cấu hình => Skipped, tuyệt đối không tính là đã kiểm chứng.
/// </summary>
public sealed class MinioStorageIntegrationTests : IAsyncLifetime
{
    private static readonly byte[] JpegContent = CreateJpegContent();

    private readonly List<string> uploadedObjectNames = new();
    private readonly List<LogEntry> initializerLogEntries = new();

    private ServiceProvider? provider;
    private HttpClient? httpClient;

    private IFileStorageService Storage => provider!.GetRequiredService<IFileStorageService>();

    public Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new CollectingLoggerProvider(initializerLogEntries)));
        services.AddInfrastructure(MinioTestEnvironment.BuildConfiguration());

        provider = services.BuildServiceProvider();
        httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Dọn object đã upload; lỗi dọn dẹp không được che kết quả test.
        foreach (var objectName in uploadedObjectNames)
        {
            try
            {
                await Storage.DeleteAsync(objectName);
            }
            catch (Exception)
            {
                // Bỏ qua: object có thể đã bị xóa bởi chính test.
            }
        }

        httpClient?.Dispose();
        provider?.Dispose();
    }

    [MinioFact]
    public async Task StartupInitializer_ShouldEnsureBucketAndApplyPolicyBeforeAnonymousGet()
    {
        // Act: chạy đúng initializer của production trước khi báo storage sẵn sàng.
        await EnsureStorageReadyAsync();

        // Assert: 404 (chứ không phải 403) là bằng chứng anonymous GET đã được policy cho phép
        // ở tầng bucket rồi mới thất bại ở bước tra object. Nếu initializer không áp được policy
        // thì request bị chặn ngay bằng 403 - đúng triệu chứng của lần chạy thử đầu tiên.
        var response = await AnonymousGetAsync(BuildObjectUrl($"categories/missing-{Guid.NewGuid():N}.jpg"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [MinioFact]
    public async Task UploadValidJpeg_ShouldStoreObjectRetrievableAnonymously()
    {
        // Arrange
        await EnsureStorageReadyAsync();

        // Act
        var result = await UploadJpegAsync();

        // Assert
        result.ObjectName.Should().StartWith($"{StorageFolders.Categories}/");
        result.ObjectName.Should().EndWith(".jpg");
        result.ContentType.Should().Be("image/jpeg");
        result.SizeBytes.Should().Be(JpegContent.Length);
        result.Url.Should().EndWith(result.ObjectName);

        var response = await AnonymousGetAsync(result.Url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("image/jpeg");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(JpegContent);
    }

    [MinioFact]
    public async Task AnonymousPut_ShouldBeForbidden()
    {
        // Arrange
        await EnsureStorageReadyAsync();
        var url = BuildObjectUrl($"{StorageFolders.Categories}/anonymous-write-{Guid.NewGuid():N}.jpg");

        // Act: PUT ẩn danh trực tiếp lên MinIO, không qua API có xác thực.
        using var content = new ByteArrayContent(JpegContent);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var response = await httpClient!.PutAsync(url, content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [MinioFact]
    public async Task AnonymousDelete_ShouldBeForbiddenAndKeepObject()
    {
        // Arrange
        await EnsureStorageReadyAsync();
        var uploaded = await UploadJpegAsync();

        // Act: DELETE ẩn danh trực tiếp lên MinIO.
        var deleteResponse = await httpClient!.DeleteAsync(uploaded.Url);

        // Assert: bị từ chối và object vẫn còn đọc được.
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AnonymousGetAsync(uploaded.Url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [MinioFact]
    public async Task DeleteAsync_ObjectThatNeverExisted_ShouldCompleteIdempotently()
    {
        // Arrange
        await EnsureStorageReadyAsync();
        var objectName = $"{StorageFolders.Categories}/never-existed-{Guid.NewGuid():N}.jpg";

        // Act & Assert: S3/MinIO coi xóa object không tồn tại là thành công.
        var act = () => Storage.DeleteAsync(objectName);

        await act.Should().NotThrowAsync();

        // Object không tồn tại: 404 NoSuchKey xác nhận policy đã hiệu lực (403 sẽ nghĩa là policy chưa được áp).
        (await AnonymousGetAsync(BuildObjectUrl(objectName))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [MinioFact]
    public async Task DeleteAsync_SameObjectTwice_ShouldCompleteIdempotently()
    {
        // Arrange
        await EnsureStorageReadyAsync();
        var uploaded = await UploadJpegAsync();

        // Act
        var act = async () =>
        {
            await Storage.DeleteAsync(uploaded.ObjectName);
            await Storage.DeleteAsync(uploaded.ObjectName);
        };

        // Assert: lần xóa thứ hai không throw và object đã biến mất
        // (anonymous GET trả 404 NoSuchKey - policy vẫn hiệu lực sau khi xóa).
        await act.Should().NotThrowAsync();
        uploadedObjectNames.Remove(uploaded.ObjectName);
        (await AnonymousGetAsync(uploaded.Url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<FileUploadResult> UploadJpegAsync()
    {
        var request = new FileUploadRequest(
            new MemoryStream(JpegContent),
            "integration-photo.jpg",
            "image/jpeg",
            JpegContent.LongLength,
            StorageFolders.Categories);

        var result = await Storage.UploadAsync(request);
        uploadedObjectNames.Add(result.ObjectName);

        return result;
    }

    private async Task EnsureStorageReadyAsync()
    {
        var initializer = provider!.GetServices<IHostedService>()
            .OfType<StorageStartupInitializer>()
            .Single();

        initializerLogEntries.Clear();

        // StorageStartupInitializer nuốt lỗi MinIO thành Warning (non-fatal). Với integration test,
        // lỗi đó nghĩa là bucket/policy chưa sẵn sàng, nên phải fail kèm nguyên nhân thật thay vì
        // để test thất bại mơ hồ ở bước anonymous GET.
        await initializer.StartAsync(CancellationToken.None);

        var readinessFailures = initializerLogEntries
            .Where(entry => entry.Level >= LogLevel.Warning)
            .ToList();

        readinessFailures.Should().BeEmpty(
            "MinIO startup initializer phải ensure bucket và áp policy thành công; "
            + string.Join(" | ", readinessFailures.Select(entry => entry.Message)));
    }

    private async Task<HttpResponseMessage> AnonymousGetAsync(string url)
        => await httpClient!.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

    private static string BuildObjectUrl(string objectName) => $"{MinioTestEnvironment.PublicBaseUrl}/{objectName}";

    private static byte[] CreateJpegContent()
    {
        // Header JPEG tối thiểu đủ để vượt magic-bytes validation; MinIO lưu nguyên byte.
        var bytes = new byte[64];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;
        bytes[3] = 0xE0;

        return bytes;
    }
}

/// <summary>Provider ghi lại mọi log entry của initializer để integration test kiểm chứng readiness.</summary>
internal sealed class CollectingLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> entries;

    public CollectingLoggerProvider(List<LogEntry> entries)
    {
        this.entries = entries;
    }

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(entries);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger : ILogger
    {
        private readonly List<LogEntry> entries;

        public CollectingLogger(List<LogEntry> entries)
        {
            this.entries = entries;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
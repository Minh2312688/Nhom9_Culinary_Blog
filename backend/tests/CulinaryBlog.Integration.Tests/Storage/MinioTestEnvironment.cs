using Microsoft.Extensions.Configuration;
using Xunit;

namespace CulinaryBlog.Integration.Tests.Storage;

/// <summary>
/// Cấu hình MinIO thật cho integration test, đọc duy nhất từ biến môi trường MINIO_TEST_*.
/// Không hardcode credential và không đọc file .env; nếu thiếu cấu hình thì test bị Skip
/// thay vì báo pass giả.
/// </summary>
internal static class MinioTestEnvironment
{
    public const string EndpointVariable = "MINIO_TEST_ENDPOINT";
    public const string AccessKeyVariable = "MINIO_TEST_ACCESS_KEY";
    public const string SecretKeyVariable = "MINIO_TEST_SECRET_KEY";
    public const string PublicBaseUrlVariable = "MINIO_TEST_PUBLIC_BASE_URL";
    public const string BucketVariable = "MINIO_TEST_BUCKET";

    /// <summary>Bucket riêng cho integration test, tránh áp public-read policy lên bucket dev.</summary>
    public const string DefaultBucket = "culinary-blog-it";

    private static readonly string? EndpointValue = Read(EndpointVariable);
    private static readonly string? AccessKeyValue = Read(AccessKeyVariable);
    private static readonly string? SecretKeyValue = Read(SecretKeyVariable);

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(EndpointValue)
        && !string.IsNullOrWhiteSpace(AccessKeyValue)
        && !string.IsNullOrWhiteSpace(SecretKeyValue);

    public static string SkipReason =>
        $"MinIO integration tests require {EndpointVariable}, {AccessKeyVariable} and {SecretKeyVariable} " +
        "in the test process environment; they were not verified against a real MinIO.";

    public static string Bucket => Read(BucketVariable) ?? DefaultBucket;

    /// <summary>Endpoint MinIO không kèm scheme, ví dụ localhost:9000 (đúng định dạng MinIO SDK cần).</summary>
    public static string Host => RemoveScheme(EndpointValue!);

    public static bool UseSsl =>
        (EndpointValue ?? string.Empty).StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>Base URL public dùng cho anonymous HTTP GET/PUT/DELETE, ví dụ http://localhost:9000/culinary-blog-it.</summary>
    public static string PublicBaseUrl
    {
        get
        {
            var configured = Read(PublicBaseUrlVariable);

            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.TrimEnd('/');
            }

            return $"{(UseSsl ? "https" : "http")}://{Host}/{Bucket}";
        }
    }

    /// <summary>
    /// Cấu hình in-memory cho AddInfrastructure: giá trị Postgres/JWT là hằng số test,
    /// chỉ để vượt bước kiểm tra cấu hình bắt buộc, không phải credential thật.
    /// </summary>
    public static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "ConnectionStrings:Postgres", "Host=localhost;Port=5432;Database=minio_it;Username=it;Password=it" },
            { "Jwt:Key", "IntegrationTestOnlySigningKey_NotARealCredential_0123456789" },
            { "MinIO:Endpoint", Host },
            { "MinIO:AccessKey", AccessKeyValue },
            { "MinIO:SecretKey", SecretKeyValue },
            { "MinIO:UseSSL", UseSsl ? "true" : "false" },
            { "MinIO:BucketName", Bucket },
            { "MinIO:PublicBaseUrl", PublicBaseUrl }
        })
        .Build();

    private static string RemoveScheme(string value)
    {
        const string httpsPrefix = "https://";
        const string httpPrefix = "http://";

        if (value.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[httpsPrefix.Length..].TrimEnd('/');
        }

        if (value.StartsWith(httpPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[httpPrefix.Length..].TrimEnd('/');
        }

        return value.TrimEnd('/');
    }

    private static string? Read(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

/// <summary>
/// Fact chỉ chạy khi có MinIO thật được cấu hình; nếu không thì runner báo Skipped
/// (không dùng Assert để tránh biến "chưa kiểm chứng" thành "đã pass").
/// </summary>
public sealed class MinioFactAttribute : FactAttribute
{
    public MinioFactAttribute()
    {
        if (!MinioTestEnvironment.IsConfigured)
        {
            Skip = MinioTestEnvironment.SkipReason;
        }
    }
}
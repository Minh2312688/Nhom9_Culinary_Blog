using System.Text.Json;
using CulinaryBlog.Infrastructure.Storage;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Storage;

/// <summary>
/// Unit test cho StorageStartupInitializer: policy public-read chỉ cấp s3:GetObject,
/// bucket được ensure trước khi áp policy, lỗi MinIO là non-fatal, và cancellation
/// của host không bị nuốt như lỗi kết nối. Không cần MinIO thật.
/// </summary>
public class StorageStartupInitializerTests
{
    /// <summary>Bucket không mặc định để phát hiện resource bị hardcode theo giá trị default.</summary>
    private const string TestBucket = "test-bucket-not-default";

    [Fact]
    public async Task StartAsync_ShouldEnsureBucketBeforeApplyingPolicy()
    {
        // Arrange
        var client = new RecordingObjectStorageClient();
        var initializer = CreateInitializer(client);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        client.CallLog.Should().Equal(
            $"EnsureBucketExists:{TestBucket}",
            $"EnsureBucketPolicy:{TestBucket}");
        client.PolicyCalls.Should().ContainSingle();
        client.PolicyCalls[0].Bucket.Should().Be(TestBucket);
    }

    [Fact]
    public async Task StartAsync_GeneratedPolicy_ShouldBeValidJsonWithSinglePublicReadStatement()
    {
        // Act
        var policyJson = await CapturePolicyJsonAsync();

        // Assert
        using var document = JsonDocument.Parse(policyJson);
        var root = document.RootElement;

        root.GetProperty("Version").GetString().Should().Be("2012-10-17");

        var statements = root.GetProperty("Statement");
        statements.ValueKind.Should().Be(JsonValueKind.Array);
        statements.GetArrayLength().Should().Be(1);

        var statement = statements[0];
        statement.GetProperty("Sid").GetString().Should().Be("PublicReadGetObjectOnly");
        statement.GetProperty("Effect").GetString().Should().Be("Allow");
        statement.GetProperty("Principal").GetString().Should().Be("*");
    }

    [Fact]
    public async Task StartAsync_GeneratedPolicy_ShouldGrantOnlyGetObjectAction()
    {
        // Act
        var policyJson = await CapturePolicyJsonAsync();

        // Assert
        using var document = JsonDocument.Parse(policyJson);
        var actions = document.RootElement
            .GetProperty("Statement")[0]
            .GetProperty("Action")
            .EnumerateArray()
            .Select(action => action.GetString())
            .ToList();

        actions.Should().Equal("s3:GetObject");
        actions.Should().NotContain(action => action!.Contains("Put")
            || action.Contains("Delete")
            || action.Contains("ListBucket")
            || action.Contains("*"));
    }

    [Theory]
    [InlineData("s3:PutObject")]
    [InlineData("s3:DeleteObject")]
    [InlineData("s3:DeleteBucket")]
    [InlineData("s3:ListBucket")]
    [InlineData("s3:*")]
    public async Task StartAsync_GeneratedPolicy_ShouldNotContainWriteOrDeletePermissions(string forbiddenAction)
    {
        // Act
        var policyJson = await CapturePolicyJsonAsync();

        // Assert
        policyJson.Should().NotContain(forbiddenAction);
    }

    [Fact]
    public async Task StartAsync_GeneratedPolicy_ShouldScopeResourceToConfiguredBucket()
    {
        // Act
        var policyJson = await CapturePolicyJsonAsync();

        // Assert
        using var document = JsonDocument.Parse(policyJson);
        var resources = document.RootElement
            .GetProperty("Statement")[0]
            .GetProperty("Resource")
            .EnumerateArray()
            .Select(resource => resource.GetString())
            .ToList();

        resources.Should().Equal($"arn:aws:s3:::{TestBucket}/*");

        // Resource phải lấy từ bucket truyền vào, không phải giá trị mặc định hardcode.
        policyJson.Should().NotContain("culinary-blog");
    }

    [Fact]
    public async Task StartAsync_WhenStorageFails_ShouldLogWarningAndNotFailStartup()
    {
        // Arrange: MinIO chưa sẵn sàng ở bước ensure bucket
        var client = new RecordingObjectStorageClient(
            new InvalidOperationException("MinIO is unreachable."));
        var logger = new RecordingStorageLogger();
        var initializer = new StorageStartupInitializer(client, logger, TestBucket);

        // Act
        var act = () => initializer.StartAsync(CancellationToken.None);

        // Assert: startup không bị chặn, chỉ log Warning và không cố áp policy
        await act.Should().NotThrowAsync();
        logger.Warnings.Should().ContainSingle();
        logger.Warnings.Single().Message.Should().Contain(TestBucket);
        logger.Warnings.Single().Exception.Should().BeOfType<InvalidOperationException>();
        client.PolicyCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_WhenHostTokenCancelled_ShouldRethrowOperationCanceledException()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var client = new RecordingObjectStorageClient();
        var logger = new RecordingStorageLogger();
        var initializer = new StorageStartupInitializer(client, logger, TestBucket);

        // Act
        var act = () => initializer.StartAsync(cancellation.Token);

        // Assert: cancellation của host được truyền tiếp, không bị log như lỗi kết nối
        await act.Should().ThrowAsync<OperationCanceledException>();
        logger.Entries.Should().BeEmpty();
        client.CallLog.Should().BeEmpty();
        client.PolicyCalls.Should().BeEmpty();
    }

    private static StorageStartupInitializer CreateInitializer(RecordingObjectStorageClient client)
        => new(client, new RecordingStorageLogger(), TestBucket);

    private static async Task<string> CapturePolicyJsonAsync()
    {
        var client = new RecordingObjectStorageClient();
        var initializer = CreateInitializer(client);

        await initializer.StartAsync(CancellationToken.None);

        client.PolicyCalls.Should().ContainSingle();

        return client.PolicyCalls[0].PolicyJson;
    }
}

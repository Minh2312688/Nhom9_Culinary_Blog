using System.Text;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Infrastructure.Storage;
using FluentAssertions;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Storage;

public sealed class MinioFileStorageServiceTests
{
    [Fact]
    public async Task UploadAsync_ShouldEnsureBucketAndUploadValidatedObject()
    {
        var client = new RecordingObjectStorageClient();
        var service = new MinioFileStorageService(
            client,
            new FileValidationService(),
            "culinary-blog",
            "https://storage.example.test");
        var content = TestImageFixtures.Jpeg;
        var request = TestImageFixtures.CreateRequest(
            content,
            "photo.jpg",
            "image/jpeg",
            folder: StorageFolders.Categories);

        var result = await service.UploadAsync(request);

        client.EnsureBucketCalls.Should().ContainSingle("culinary-blog");
        client.Uploads.Should().ContainSingle();
        client.Uploads[0].Bucket.Should().Be("culinary-blog");
        client.Uploads[0].ContentType.Should().Be("image/jpeg");
        client.Uploads[0].SizeBytes.Should().Be(content.Length);
        client.Uploads[0].Content.Should().Equal(content);
        result.ObjectName.Should().StartWith("categories/");
        result.ObjectName.Should().EndWith(".jpg");
        result.Url.Should().Be($"https://storage.example.test/{result.ObjectName}");
        result.SizeBytes.Should().Be(content.Length);
    }

    [Fact]
    public async Task UploadAsync_ShouldRejectInvalidFileBeforeCallingStorage()
    {
        var client = new RecordingObjectStorageClient();
        var service = new MinioFileStorageService(
            client,
            new FileValidationService(),
            "culinary-blog",
            "https://storage.example.test");
        var request = TestImageFixtures.CreateRequest(
            Encoding.UTF8.GetBytes("not an image"),
            "photo.jpg",
            "image/jpeg",
            folder: StorageFolders.Categories);

        var act = () => service.UploadAsync(request);

        await act.Should().ThrowAsync<InvalidFileException>();
        client.EnsureBucketCalls.Should().BeEmpty();
        client.Uploads.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ShouldEnsureBucketAndDeleteObject()
    {
        var client = new RecordingObjectStorageClient();
        var service = new MinioFileStorageService(
            client,
            new FileValidationService(),
            "culinary-blog",
            "https://storage.example.test");

        await service.DeleteAsync("categories/image.jpg");

        client.EnsureBucketCalls.Should().ContainSingle("culinary-blog");
        client.DeletedObjects.Should().ContainSingle("categories/image.jpg");
    }

    [Fact]
    public async Task DeleteAsync_ShouldExtractObjectNameFromStorageUrl()
    {
        var client = new RecordingObjectStorageClient();
        var service = new MinioFileStorageService(
            client,
            new FileValidationService(),
            "culinary-blog",
            "https://storage.example.test/culinary-blog");

        await service.DeleteAsync("https://storage.example.test/culinary-blog/categories/image.jpg");

        client.DeletedObjects.Should().ContainSingle("categories/image.jpg");
    }

    private sealed class RecordingObjectStorageClient : IObjectStorageClient
    {
        public List<string> EnsureBucketCalls { get; } = [];
        public List<UploadCall> Uploads { get; } = [];
        public List<string> DeletedObjects { get; } = [];

        public Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken)
        {
            EnsureBucketCalls.Add(bucketName);
            return Task.CompletedTask;
        }

        public async Task UploadAsync(
            string bucketName,
            string objectName,
            Stream content,
            long sizeBytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await content.CopyToAsync(memory, cancellationToken);
            Uploads.Add(new UploadCall(
                bucketName,
                objectName,
                memory.ToArray(),
                sizeBytes,
                contentType));
        }

        public Task DeleteAsync(
            string bucketName,
            string objectName,
            CancellationToken cancellationToken)
        {
            DeletedObjects.Add(objectName);
            return Task.CompletedTask;
        }
    }

    private sealed record UploadCall(
        string Bucket,
        string ObjectName,
        byte[] Content,
        long SizeBytes,
        string ContentType);
}

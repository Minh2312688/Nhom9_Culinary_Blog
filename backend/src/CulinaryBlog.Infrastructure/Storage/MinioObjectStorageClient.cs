using Minio;
using Minio.DataModel.Args;

namespace CulinaryBlog.Infrastructure.Storage;

internal sealed class MinioObjectStorageClient : IObjectStorageClient
{
    private readonly IMinioClient client;

    public MinioObjectStorageClient(IMinioClient client)
    {
        this.client = client;
    }

    public async Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken)
    {
        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName),
            cancellationToken);

        if (!exists)
        {
            await client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName),
                cancellationToken);
        }
    }

    public Task UploadAsync(
        string bucketName,
        string objectName,
        Stream content,
        long sizeBytes,
        string contentType,
        CancellationToken cancellationToken)
        => client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithStreamData(content)
                .WithObjectSize(sizeBytes)
                .WithContentType(contentType),
            cancellationToken);

    public Task DeleteAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken)
        => client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName),
            cancellationToken);
}

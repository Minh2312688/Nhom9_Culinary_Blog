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

    public Task EnsureBucketPolicyAsync(
        string bucketName,
        string policyJson,
        CancellationToken cancellationToken)
        => client.SetPolicyAsync(
            new SetPolicyArgs()
                .WithBucket(bucketName)
                .WithPolicy(policyJson),
            cancellationToken);

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

    public async Task<Stream> DownloadAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken)
    {
        var content = new MemoryStream();
        try
        {
            await client.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithCallbackStream(stream => stream.CopyTo(content)),
                cancellationToken);

            content.Position = 0;
            return content;
        }
        catch
        {
            await content.DisposeAsync();
            throw;
        }
    }

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

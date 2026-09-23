namespace CulinaryBlog.Infrastructure.Storage;

public interface IObjectStorageClient
{
    Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken);

    Task UploadAsync(
        string bucketName,
        string objectName,
        Stream content,
        long sizeBytes,
        string contentType,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string bucketName,
        string objectName,
        CancellationToken cancellationToken);
}

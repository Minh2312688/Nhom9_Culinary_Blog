using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts.Storage;

namespace CulinaryBlog.Infrastructure.Storage;

public sealed class MinioFileStorageService : IFileStorageService
{
    private readonly IObjectStorageClient client;
    private readonly IFileValidationService validator;
    private readonly string bucketName;
    private readonly string publicBaseUrl;

    public MinioFileStorageService(
        IObjectStorageClient client,
        IFileValidationService validator,
        string bucketName,
        string publicBaseUrl)
    {
        this.client = client;
        this.validator = validator;
        this.bucketName = RequireValue(bucketName, nameof(bucketName));
        this.publicBaseUrl = RequireValue(publicBaseUrl, nameof(publicBaseUrl)).TrimEnd('/');
    }

    public async Task<FileUploadResult> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(request);
        var objectName = ObjectNameFactory.Create(request.Folder, validation.Format);

        await client.EnsureBucketExistsAsync(bucketName, cancellationToken);
        request.Content.Position = 0;
        await client.UploadAsync(
            bucketName,
            objectName,
            request.Content,
            validation.SizeBytes,
            validation.ContentType,
            cancellationToken);

        return new FileUploadResult(
            objectName,
            BuildUrl(objectName),
            validation.ContentType,
            validation.SizeBytes);
    }

    public async Task DeleteAsync(
        string fileUrl,
        CancellationToken cancellationToken = default)
    {
        var objectName = ExtractObjectName(fileUrl);

        await client.EnsureBucketExistsAsync(bucketName, cancellationToken);
        await client.DeleteAsync(bucketName, objectName, cancellationToken);
    }

    private string BuildUrl(string objectName)
        => string.IsNullOrEmpty(publicBaseUrl)
            ? objectName
            : $"{publicBaseUrl}/{objectName}";

    private string ExtractObjectName(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            throw new ArgumentException("File URL or object name is required.", nameof(fileUrl));
        }

        var value = fileUrl.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) &&
                (!string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
                 uri.Port != baseUri.Port))
            {
                throw new ArgumentException("File URL does not belong to the configured storage endpoint.", nameof(fileUrl));
            }

            value = uri.AbsolutePath.Trim('/');
            if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out baseUri))
            {
                var basePath = baseUri.AbsolutePath.Trim('/');
                if (!string.IsNullOrEmpty(basePath) &&
                    value.StartsWith($"{basePath}/", StringComparison.OrdinalIgnoreCase))
                {
                    value = value[(basePath.Length + 1)..];
                }
            }
        }

        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('\\') ||
            value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("File URL must contain a valid object name.", nameof(fileUrl));
        }

        return value;
    }

    private static string RequireValue(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}

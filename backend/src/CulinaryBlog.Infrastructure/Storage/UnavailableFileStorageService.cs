using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Storage;

namespace CulinaryBlog.Infrastructure.Storage;

public sealed class UnavailableFileStorageService : IFileStorageService
{
    public Task<FileUploadResult> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
        => throw new StorageUnavailableException("Object storage is not configured.");

    public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        => throw new StorageUnavailableException("Object storage is not configured.");
}
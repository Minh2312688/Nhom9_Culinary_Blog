using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Domain.Entities;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Features.Categories.Commands.UploadCategoryImage;

public sealed record UploadCategoryImageCommand(
    Guid CategoryId,
    Stream Content,
    string FileName,
    string? ContentType,
    long Length) : IRequest<CategoryDto>;

public sealed class UploadCategoryImageCommandHandler(
    IApplicationDbContext context,
    ICategoryCache cache,
    IFileStorageService storage,
    ILogger<UploadCategoryImageCommandHandler> logger)
    : IRequestHandler<UploadCategoryImageCommand, CategoryDto>
{
    public async Task<CategoryDto> Handle(
        UploadCategoryImageCommand request,
        CancellationToken cancellationToken)
    {
        var category = await context.Categories
            .SingleOrDefaultAsync(item => item.Id == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Category), request.CategoryId);

        var uploaded = await storage.UploadAsync(new FileUploadRequest(
            request.Content,
            request.FileName,
            request.ContentType,
            request.Length,
            StorageFolders.Categories), cancellationToken);

        category.ImageUrl = uploaded.Url;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await storage.DeleteAsync(uploaded.Url, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                logger.LogError(cleanupException,
                    "Failed to remove category image {ImageUrl} after database save failed.",
                    uploaded.Url);
            }

            throw;
        }

        await cache.InvalidateAsync(cancellationToken);
        return category.Adapt<CategoryDto>();
    }
}

using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.Features.Categories.Commands.UploadCategoryImage;
using CulinaryBlog.Application.Tests.Features.Categories;
using CulinaryBlog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Categories.Commands.UploadCategoryImage;

public sealed class UploadCategoryImageCommandHandlerTests
{
    [Fact]
    public async Task Handle_uploads_image_persists_url_and_invalidates_category_cache()
    {
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Món canh");
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        var cache = new FakeCategoryCache();
        var storage = CreateStorageMock();
        var handler = CreateHandler(context, storage.Object, cache);

        var result = await handler.Handle(CreateCommand(category.Id), CancellationToken.None);

        Assert.Equal("https://media.example.test/categories/mon-canh.webp", result.ImageUrl);
        Assert.Equal(result.ImageUrl, (await context.Categories.SingleAsync()).ImageUrl);
        Assert.Equal(1, cache.InvalidateCount);
        storage.Verify(service => service.UploadAsync(
            It.Is<FileUploadRequest>(request => request.Folder == "categories"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_unknown_category_does_not_upload_image()
    {
        await using var context = CategoryTestDbContext.CreateInMemory();
        var storage = CreateStorageMock();
        var handler = CreateHandler(context, storage.Object, new FakeCategoryCache());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None));

        storage.Verify(service => service.UploadAsync(
            It.IsAny<FileUploadRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_database_failure_deletes_uploaded_object_and_rethrows()
    {
        await using var context = CategoryTestDbContext.CreateInMemory();
        var category = Category.Create("Món canh");
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var database = new Mock<IApplicationDbContext>();
        database.SetupGet(value => value.Categories).Returns(context.Categories);
        database.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Database unavailable."));
        var storage = CreateStorageMock();
        var handler = CreateHandler(database.Object, storage.Object, new FakeCategoryCache());

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            handler.Handle(CreateCommand(category.Id), CancellationToken.None));

        storage.Verify(service => service.DeleteAsync(
            "https://media.example.test/categories/mon-canh.webp",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UploadCategoryImageCommandHandler CreateHandler(
        IApplicationDbContext context,
        IFileStorageService storage,
        FakeCategoryCache cache) =>
        new(context, cache, storage, NullLogger<UploadCategoryImageCommandHandler>.Instance);

    private static Mock<IFileStorageService> CreateStorageMock()
    {
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.UploadAsync(
                It.IsAny<FileUploadRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult(
                "categories/mon-canh.webp",
                "https://media.example.test/categories/mon-canh.webp",
                "image/webp",
                1));
        storage.Setup(service => service.DeleteAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return storage;
    }

    private static UploadCategoryImageCommand CreateCommand(Guid categoryId) =>
        new(categoryId, new MemoryStream([1]), "mon-canh.webp", "image/webp", 1);
}

using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Persistence;
using CulinaryBlog.Application.Contracts.Recipes;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class RecipeImageCommandHandlerTests
{
    [Fact]
    public async Task RecipeDifficulty_UsesStringStorageConversion()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync();

        var property = fixture.Db.Model.FindEntityType(typeof(Recipe))!
            .FindProperty(nameof(Recipe.Difficulty))!;

        Assert.Equal(typeof(string), property.GetTypeMapping().Converter?.ProviderClrType);
    }

    [Fact]
    public async Task Upload_FirstImage_SavesAsPrimaryAndReturnsSelectedContract()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync();
        var storage = CreateStorageMock();
        var cache = new RecordingRecipeCache();
        var enqueuer = new RecordingImageJobEnqueuer(fixture.Db);
        var handler = new UploadRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage.Object, cache, enqueuer,
            NullLogger<UploadRecipeImageCommandHandler>.Instance);

        var response = await handler.Handle(UploadCommand(fixture.Recipe.Id), CancellationToken.None);
        var savedImage = await fixture.Db.RecipeImages.SingleAsync();

        Assert.Equal(savedImage.Id, response.ImageId);
        Assert.Equal("https://storage/recipes/photo.jpg", response.OriginalUrl);
        Assert.True(response.IsPrimary);
        Assert.Equal(0, savedImage.OrderIndex);
        Assert.Contains("recipes:", cache.RemovedPrefixes);
        Assert.Equal([savedImage.Id], enqueuer.EnqueuedIds);
        Assert.True(enqueuer.WasPersistedBeforeEnqueue);
    }

    [Fact]
    public async Task Upload_WhenEnqueueFails_StillReturnsPersistedOriginalImage()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync();
        var storage = CreateStorageMock();
        var enqueuer = new RecordingImageJobEnqueuer(fixture.Db)
        {
            Failure = new InvalidOperationException("Hangfire unavailable")
        };
        var handler = new UploadRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage.Object, new RecordingRecipeCache(), enqueuer,
            NullLogger<UploadRecipeImageCommandHandler>.Instance);

        var response = await handler.Handle(UploadCommand(fixture.Recipe.Id), CancellationToken.None);

        Assert.Equal("https://storage/recipes/photo.jpg", response.OriginalUrl);
        Assert.Equal(response.ImageId, (await fixture.Db.RecipeImages.SingleAsync()).Id);
        Assert.Equal([response.ImageId], enqueuer.EnqueuedIds);
    }

    [Fact]
    public async Task Upload_WhenDatabaseSaveFails_DoesNotEnqueueAndCleansUpOriginal()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync();
        fixture.Db.FailNextSave = true;
        var storage = CreateStorageMock();
        var enqueuer = new RecordingImageJobEnqueuer(fixture.Db);
        var handler = new UploadRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage.Object, new RecordingRecipeCache(), enqueuer,
            NullLogger<UploadRecipeImageCommandHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(UploadCommand(fixture.Recipe.Id), CancellationToken.None));

        Assert.Empty(enqueuer.EnqueuedIds);
        storage.Verify(service => service.DeleteAsync(
            "https://storage/recipes/photo.jpg", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upload_EleventhImage_IsRejectedBeforeStorageCall()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 10);
        var storage = CreateStorageMock();
        var handler = new UploadRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage.Object, new RecordingRecipeCache(),
            new RecordingImageJobEnqueuer(fixture.Db),
            NullLogger<UploadRecipeImageCommandHandler>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(UploadCommand(fixture.Recipe.Id), CancellationToken.None));

        storage.Verify(service => service.UploadAsync(
            It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetPrimaryImage_ClearsPreviousPrimary()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 2);
        var images = await fixture.Db.RecipeImages.OrderBy(image => image.OrderIndex).ToListAsync();
        var handler = new SetPrimaryRecipeImageCommandHandler(
            fixture.Db, fixture.User, new RecordingRecipeCache());

        var response = await handler.Handle(
            new SetPrimaryRecipeImageCommand(fixture.Recipe.Id, images[1].Id), CancellationToken.None);

        Assert.Equal(images[1].Id, response.Id);
        Assert.False(images[0].IsPrimary);
        Assert.True(images[1].IsPrimary);
    }

    [Fact]
    public async Task DeletePrimaryImage_PromotesNextImageAndSoftDeletesSelected()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 2);
        var images = await fixture.Db.RecipeImages.OrderBy(image => image.OrderIndex).ToListAsync();
        var storage = CreateStorageMock();
        var handler = new DeleteRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage.Object, new RecordingRecipeCache());

        await handler.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, images[0].Id), CancellationToken.None);

        Assert.True(images[1].IsPrimary);
        Assert.True(await fixture.Db.RecipeImages.IgnoreQueryFilters()
            .Where(image => image.Id == images[0].Id).Select(image => image.IsDeleted).SingleAsync());
        storage.Verify(service => service.DeleteAsync(images[0].OriginalUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteOnlyPrimaryImage_IsRejectedWithoutDeletingObject()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 1);
        var image = await fixture.Db.RecipeImages.SingleAsync();
        var storage = CreateStorageMock();
        var handler = new DeleteRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage.Object, new RecordingRecipeCache());

        await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, image.Id), CancellationToken.None));

        storage.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteImage_DeletesOriginalMediumAndThumbnailObjects()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 2);
        var images = await fixture.Db.RecipeImages.OrderBy(image => image.OrderIndex).ToListAsync();
        images[0].MediumUrl = "https://storage/image-0-medium.jpg";
        images[0].ThumbnailUrl = "https://storage/image-0-thumb.jpg";
        await fixture.Db.SaveChangesAsync();

        var storage = new RecordingStorage();
        var handler = new DeleteRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage, new RecordingRecipeCache());

        await handler.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, images[0].Id), CancellationToken.None);

        Assert.Equal(new[]
        {
            "https://storage/image-0.jpg",
            "https://storage/image-0-medium.jpg",
            "https://storage/image-0-thumb.jpg"
        }, storage.Deleted);
        Assert.True(await fixture.Db.RecipeImages.IgnoreQueryFilters()
            .Where(image => image.Id == images[0].Id).Select(image => image.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task DeleteImage_WhenVariantUrlsMissing_DeletesOnlyOriginalObject()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 2);
        var images = await fixture.Db.RecipeImages.OrderBy(image => image.OrderIndex).ToListAsync();
        images[0].MediumUrl = string.Empty;
        images[0].ThumbnailUrl = null;
        await fixture.Db.SaveChangesAsync();

        var storage = new RecordingStorage();
        var handler = new DeleteRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage, new RecordingRecipeCache());

        await handler.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, images[0].Id), CancellationToken.None);

        Assert.Equal(new[] { images[0].OriginalUrl }, storage.Deleted);
    }

    [Fact]
    public async Task DeleteImage_WhenOneObjectDeleteFails_AttemptsRemainingAndLeavesRowForRetry()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 2);
        var images = await fixture.Db.RecipeImages.OrderBy(image => image.OrderIndex).ToListAsync();
        images[0].MediumUrl = "https://storage/image-0-medium.jpg";
        images[0].ThumbnailUrl = "https://storage/image-0-thumb.jpg";
        await fixture.Db.SaveChangesAsync();

        var storage = new RecordingStorage { FailUrl = "https://storage/image-0-medium.jpg" };
        var handler = new DeleteRecipeImageCommandHandler(
            fixture.Db, fixture.User, storage, new RecordingRecipeCache());

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            handler.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, images[0].Id), CancellationToken.None));

        Assert.Contains("https://storage/image-0-thumb.jpg", storage.Deleted);
        Assert.False(await fixture.Db.RecipeImages.IgnoreQueryFilters()
            .Where(image => image.Id == images[0].Id).Select(image => image.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task DeleteImage_WhenSaveFailsAfterStorageDelete_LeavesRowForRetry()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 2);
        var images = await fixture.Db.RecipeImages.OrderBy(image => image.OrderIndex).ToListAsync();
        images[0].MediumUrl = "https://storage/image-0-medium.jpg";
        images[0].ThumbnailUrl = "https://storage/image-0-thumb.jpg";
        await fixture.Db.SaveChangesAsync();

        var storage = new RecordingStorage();
        var cache = new RecordingRecipeCache();
        var handler = new DeleteRecipeImageCommandHandler(fixture.Db, fixture.User, storage, cache);

        fixture.Db.FailNextSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new DeleteRecipeImageCommand(fixture.Recipe.Id, images[0].Id), CancellationToken.None));

        Assert.Equal(new[]
        {
            "https://storage/image-0.jpg",
            "https://storage/image-0-medium.jpg",
            "https://storage/image-0-thumb.jpg"
        }, storage.Deleted);
        Assert.False(await fixture.Db.RecipeImages.IgnoreQueryFilters()
            .Where(image => image.Id == images[0].Id).Select(image => image.IsDeleted).SingleAsync());
        Assert.Empty(cache.RemovedPrefixes);
    }

    [Fact]
    public async Task ImageCommands_RejectNonOwner()
    {
        await using var fixture = await RecipeImageFixture.CreateAsync(imageCount: 1);
        fixture.User.Id = "another-author";
        var image = await fixture.Db.RecipeImages.SingleAsync();
        var handler = new SetPrimaryRecipeImageCommandHandler(
            fixture.Db, fixture.User, new RecordingRecipeCache());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            handler.Handle(new SetPrimaryRecipeImageCommand(fixture.Recipe.Id, image.Id), CancellationToken.None));
    }

    private static UploadRecipeImageCommand UploadCommand(Guid recipeId) => new(
        recipeId, new MemoryStream([1, 2, 3]), "photo.jpg", "image/jpeg", 3, "Photo");

    private static Mock<IFileStorageService> CreateStorageMock()
    {
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.UploadAsync(
                It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("recipes/photo.jpg", "https://storage/recipes/photo.jpg", "image/jpeg", 3));
        storage.Setup(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return storage;
    }

    private sealed class RecordingStorage : IFileStorageService
    {
        public List<string> Deleted { get; } = [];
        public string? FailUrl { get; init; }

        public Task<FileUploadResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> DownloadAsync(string fileUrl, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            Deleted.Add(fileUrl);
            if (FailUrl is not null && string.Equals(FailUrl, fileUrl, StringComparison.Ordinal))
            {
                throw new StorageUnavailableException("Object storage is unavailable.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecipeImageFixture : IAsyncDisposable
    {
        public required TestApplicationDbContext Db { get; init; }
        public required TestCurrentUser User { get; init; }
        public required Recipe Recipe { get; init; }

        public static async Task<RecipeImageFixture> CreateAsync(int imageCount = 0)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeImages_{Guid.NewGuid():N}").Options;
            var db = new TestApplicationDbContext(options);
            var user = new TestCurrentUser { Id = "recipe-owner" };
            var category = Category.Create("Soup", null);
            var recipe = new Recipe
            {
                Title = "Vegetable Soup", Slug = "vegetable-soup", CategoryId = category.Id,
                AuthorId = user.Id, Difficulty = RecipeDifficulty.Easy, Servings = 2
            };

            db.Categories.Add(category);
            db.Recipes.Add(recipe);
            for (var index = 0; index < imageCount; index++)
            {
                recipe.Images.Add(new RecipeImage
                {
                    OriginalUrl = $"https://storage/image-{index}.jpg",
                    IsPrimary = index == 0,
                    OrderIndex = index
                });
            }

            await db.SaveChangesAsync();
            return new RecipeImageFixture { Db = db, User = user, Recipe = recipe };
        }

        public sealed class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : ApplicationDbContext(options)
        {
            public bool FailNextSave { get; set; }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                if (FailNextSave)
                {
                    FailNextSave = false;
                    throw new InvalidOperationException("Simulated database failure.");
                }

                return base.SaveChangesAsync(cancellationToken);
            }
        }

        private sealed class RecordingImageJobEnqueuer(ApplicationDbContext db) : IRecipeImageJobEnqueuer
        {
            public List<Guid> EnqueuedIds { get; } = [];
            public Exception? Failure { get; init; }
            public bool WasPersistedBeforeEnqueue { get; private set; }

            public Task EnqueueAsync(Guid imageId, CancellationToken cancellationToken = default)
            {
                EnqueuedIds.Add(imageId);
                WasPersistedBeforeEnqueue = db.ChangeTracker
                    .Entries<RecipeImage>()
                    .Any(entry => entry.Entity.Id == imageId && entry.State == EntityState.Unchanged);
                if (Failure is not null)
                {
                    throw Failure;
                }

                return Task.CompletedTask;
            }
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class RecordingImageJobEnqueuer(ApplicationDbContext db) : IRecipeImageJobEnqueuer
    {
        public List<Guid> EnqueuedIds { get; } = [];
        public Exception? Failure { get; init; }
        public bool WasPersistedBeforeEnqueue { get; private set; }

        public Task EnqueueAsync(Guid imageId, CancellationToken cancellationToken = default)
        {
            EnqueuedIds.Add(imageId);
            WasPersistedBeforeEnqueue = db.ChangeTracker
                .Entries<RecipeImage>()
                .Any(entry => entry.Entity.Id == imageId && entry.State == EntityState.Unchanged);
            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? Id { get; set; }
        public string? UserId => Id;
        public bool IsAdmin { get; set; }
        public bool IsAuthenticated => Id is not null || IsAdmin;
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public List<string> RemovedPrefixes { get; } = [];
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            RemovedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }
    }
}

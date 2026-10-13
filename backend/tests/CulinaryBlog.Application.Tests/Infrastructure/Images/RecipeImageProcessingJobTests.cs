using CulinaryBlog.Application.Common.Files;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Images;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SkiaSharp;
using Xunit;

namespace CulinaryBlog.Application.Tests.Infrastructure.Images;

public sealed class RecipeImageProcessingJobTests
{
    [Fact]
    public async Task ProcessAsync_WhenSuccessful_SavesUrlsAndInvalidatesRecipeCache()
    {
        await using var fixture = await ImageJobFixture.CreateAsync();
        var storage = fixture.CreateStorage();
        storage.SetupSequence(service => service.UploadAsync(
                It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("thumb.png", "https://storage/thumb.png", "image/png", 10))
            .ReturnsAsync(new FileUploadResult("medium.png", "https://storage/medium.png", "image/png", 20));

        var job = fixture.CreateJob(storage.Object);
        await job.ProcessAsync(fixture.Image.Id, CancellationToken.None);

        fixture.Image.ThumbnailUrl.Should().Be("https://storage/thumb.png");
        fixture.Image.MediumUrl.Should().Be("https://storage/medium.png");
        fixture.Image.VariantStatus.Should().Be(RecipeImageVariantStatus.Ready);
        fixture.Image.OriginalUrl.Should().Be(fixture.OriginalUrl);
        fixture.Cache.RemovedKeys.Should().ContainSingle($"recipes:slug:{fixture.Recipe.Slug}");
        storage.Verify(service => service.UploadAsync(
            It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_WhenAvif_SetsUnsupportedWithoutUploadingOrChangingOriginal()
    {
        await using var fixture = await ImageJobFixture.CreateAsync(avif: true);
        var storage = fixture.CreateStorage();
        var job = fixture.CreateJob(storage.Object);

        await job.ProcessAsync(fixture.Image.Id, CancellationToken.None);

        fixture.Image.VariantStatus.Should().Be(RecipeImageVariantStatus.Unsupported);
        fixture.Image.ThumbnailUrl.Should().BeNull();
        fixture.Image.MediumUrl.Should().BeNull();
        fixture.Image.OriginalUrl.Should().Be(fixture.OriginalUrl);
        storage.Verify(service => service.UploadAsync(
            It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Cache.RemovedKeys.Should().ContainSingle($"recipes:slug:{fixture.Recipe.Slug}");
    }

    [Fact]
    public async Task ProcessAsync_WhenVariantUploadFails_LeavesOriginalAndVariantUrlsUnchanged()
    {
        await using var fixture = await ImageJobFixture.CreateAsync();
        var storage = fixture.CreateStorage();
        storage.SetupSequence(service => service.UploadAsync(
                It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("thumb.png", "https://storage/thumb.png", "image/png", 10))
            .ThrowsAsync(new IOException("storage unavailable"));

        var job = fixture.CreateJob(storage.Object);
        var act = () => job.ProcessAsync(fixture.Image.Id, CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        fixture.Image.OriginalUrl.Should().Be(fixture.OriginalUrl);
        fixture.Image.ThumbnailUrl.Should().BeNull();
        fixture.Image.MediumUrl.Should().BeNull();
        fixture.Image.VariantStatus.Should().Be(RecipeImageVariantStatus.Pending);
        fixture.Cache.RemovedKeys.Should().BeEmpty();
        storage.Verify(service => service.DeleteAsync(
            "https://storage/thumb.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenCacheInvalidationFails_DoesNotDeletePersistedVariants()
    {
        await using var fixture = await ImageJobFixture.CreateAsync();
        var storage = fixture.CreateStorage();
        storage.SetupSequence(service => service.UploadAsync(
                It.IsAny<FileUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("thumb.png", "https://storage/thumb.png", "image/png", 10))
            .ReturnsAsync(new FileUploadResult("medium.png", "https://storage/medium.png", "image/png", 20));
        fixture.Cache.Failure = new IOException("cache unavailable");

        var job = fixture.CreateJob(storage.Object);
        var act = () => job.ProcessAsync(fixture.Image.Id, CancellationToken.None);

        await act.Should().ThrowAsync<IOException>();
        fixture.Image.ThumbnailUrl.Should().Be("https://storage/thumb.png");
        fixture.Image.MediumUrl.Should().Be("https://storage/medium.png");
        storage.Verify(service => service.DeleteAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenImageDoesNotExist_ThrowsAndDoesNotTouchStorage()
    {
        await using var fixture = await ImageJobFixture.CreateAsync();
        var storage = fixture.CreateStorage();
        var job = fixture.CreateJob(storage.Object);

        var act = () => job.ProcessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        storage.Verify(service => service.DownloadAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Job_UsesImageProcessingQueueAndThreeRetries()
    {
        var queue = typeof(RecipeImageProcessingJob)
            .GetCustomAttributes(typeof(QueueAttribute), inherit: true)
            .Cast<QueueAttribute>()
            .Single();
        var retry = typeof(RecipeImageProcessingJob)
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: true)
            .Cast<AutomaticRetryAttribute>()
            .Single();

        queue.Queue.Should().Be("image-processing");
        retry.Attempts.Should().Be(3);
        retry.OnAttemptsExceeded.Should().Be(AttemptsExceededAction.Fail);
    }

    private sealed class ImageJobFixture : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required Recipe Recipe { get; init; }
        public required RecipeImage Image { get; init; }
        public required RecordingRecipeCache Cache { get; init; }
        public required byte[] SourceBytes { get; init; }
        public string OriginalUrl => "https://storage/original-image";

        public RecipeImageProcessingJob CreateJob(IFileStorageService storage)
            => new(Db, storage, Cache, new RecipeImageProcessor(),
                NullLogger<RecipeImageProcessingJob>.Instance);

        public Mock<IFileStorageService> CreateStorage()
        {
            var storage = new Mock<IFileStorageService>();
            storage.Setup(service => service.DownloadAsync(
                    OriginalUrl, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(SourceBytes));
            return storage;
        }

        public static async Task<ImageJobFixture> CreateAsync(bool avif = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"ImageJob_{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options);
            var category = Category.Create("Soup", null);
            var recipe = new Recipe
            {
                Title = "Vegetable Soup",
                Slug = "vegetable-soup",
                CategoryId = category.Id,
                AuthorId = "author"
            };
            var image = new RecipeImage
            {
                Recipe = recipe,
                RecipeId = recipe.Id,
                OriginalUrl = "https://storage/original-image",
                IsPrimary = true
            };
            recipe.Images.Add(image);
            db.Categories.Add(category);
            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            var sourceBytes = avif
                ? await File.ReadAllBytesAsync(Path.Combine(
                    AppContext.BaseDirectory, "TestData", "fruits_2layer_thumbsize.avif"))
                : CreatePng();
            return new ImageJobFixture
            {
                Db = db,
                Recipe = recipe,
                Image = image,
                Cache = new RecordingRecipeCache(),
                SourceBytes = sourceBytes
            };
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();

        private static byte[] CreatePng()
        {
            using var bitmap = new SKBitmap(new SKImageInfo(80, 60));
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Coral);
            using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return encoded.ToArray();
        }
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public List<string> RemovedKeys { get; } = [];
        public Exception? Failure { get; set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(default(T));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}

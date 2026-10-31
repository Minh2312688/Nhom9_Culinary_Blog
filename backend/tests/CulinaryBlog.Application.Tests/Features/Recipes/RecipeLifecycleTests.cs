using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CulinaryBlog.Application.Tests.Features.Recipes;

public sealed class RecipeLifecycleTests
{
    [Fact]
    public async Task Publish_SetsAndPersistsTheCurrentUtcTimestamp()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var handler = fixture.CreateHandler();
        var beforePublish = DateTimeOffset.UtcNow;

        await handler.Handle(new PublishRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        var afterPublish = DateTimeOffset.UtcNow;
        var persisted = await fixture.Db.Recipes.SingleAsync(recipe => recipe.Id == fixture.Recipe.Id);
        Assert.Equal(RecipeStatus.Published, persisted.Status);
        Assert.InRange(persisted.PublishedAt!.Value, beforePublish, afterPublish);
        Assert.Equal(TimeSpan.Zero, persisted.PublishedAt.Value.Offset);
    }

    [Fact]
    public async Task Archive_PreservesThePublicationTimestamp()
    {
        var publishedAt = new DateTimeOffset(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);
        await using var fixture = await RecipeLifecycleFixture.CreateAsync(RecipeStatus.Published, publishedAt);

        await fixture.CreateHandler().Handle(
            new ArchiveRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        var persisted = await fixture.Db.Recipes.SingleAsync(recipe => recipe.Id == fixture.Recipe.Id);
        Assert.Equal(RecipeStatus.Archived, persisted.Status);
        Assert.Equal(publishedAt, persisted.PublishedAt);
    }

    [Fact]
    public async Task Delete_RecipeWithImages_DeletesAllStorageObjects_AndSoftDeletesRecipe()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        fixture.AddImages(2);
        var handler = fixture.CreateHandler();
        var storage = fixture.Storage;
        storage.Deleted.Clear();

        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        // Recipe + images are soft-deleted (IsDeleted = true), not hard-deleted.
        var recipe = await fixture.Db.Recipes
            .IgnoreQueryFilters()
            .SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.True(recipe.IsDeleted);

        var images = await fixture.Db.RecipeImages
            .IgnoreQueryFilters()
            .Where(i => i.RecipeId == recipe.Id)
            .ToListAsync();
        Assert.All(images, img => Assert.True(img.IsDeleted));

        // All storage objects (original/medium/thumbnail) deleted.
        storage.Deleted.Should().HaveCount(4);
        storage.Deleted.Should().Contain("https://storage/image-0.jpg");
        storage.Deleted.Should().Contain("https://storage/image-0-medium.jpg");
        storage.Deleted.Should().Contain("https://storage/image-0-thumb.jpg");
        storage.Deleted.Should().Contain("https://storage/image-1.jpg");
    }

    [Fact]
    public async Task Delete_RecipeWithImages_InvalidatesCache()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        fixture.AddImages(1);
        var handler = fixture.CreateHandler();

        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        fixture.Cache.RemovedKeys.Should().ContainSingle($"recipes:slug:{fixture.Recipe.Slug}");
        fixture.Cache.RemovedPrefixes.Should().Contain("recipes:");
    }

    [Fact]
    public async Task Delete_WhenOneStorageObjectFails_ThrowsAndDoesNotSoftDeleteRecipe_ButInvalidatesCache()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        fixture.AddImages(2);
        fixture.Storage.FailUrl = "https://storage/image-0-medium.jpg";
        var handler = fixture.CreateHandler();

        await Assert.ThrowsAsync<StorageUnavailableException>(() =>
            handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None));

        // Recipe is NOT soft-deleted (SaveChanges never ran).
        var recipe = await fixture.Db.Recipes.SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.False(recipe.IsDeleted);

        // Images are NOT soft-deleted.
        var images = await fixture.Db.RecipeImages.Where(i => i.RecipeId == recipe.Id).ToListAsync();
        Assert.All(images, img => Assert.False(img.IsDeleted));

        // Cache is still invalidated even on failure.
        fixture.Cache.RemovedKeys.Should().ContainSingle($"recipes:slug:{fixture.Recipe.Slug}");
        fixture.Cache.RemovedPrefixes.Should().Contain("recipes:");
    }

    [Fact]
    public async Task Delete_RecipeWithNoImages_SoftDeletesRecipe_AndInvalidatesCache()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var handler = fixture.CreateHandler();

        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        var recipe = await fixture.Db.Recipes.IgnoreQueryFilters()
            .SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.True(recipe.IsDeleted);
        fixture.Cache.RemovedKeys.Should().ContainSingle($"recipes:slug:{fixture.Recipe.Slug}");
        fixture.Cache.RemovedPrefixes.Should().Contain("recipes:");
    }

    [Fact]
    public async Task Delete_RecipeWithSharedUrls_DeletesEachUniqueUrlOnlyOnce()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        fixture.AddImages(2);
        var image0 = await fixture.Db.RecipeImages.OrderBy(i => i.OrderIndex).FirstAsync();
        image0.ThumbnailUrl = image0.OriginalUrl; // Shared URL between original and thumbnail.
        await fixture.Db.SaveChangesAsync();

        var handler = fixture.CreateHandler();

        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        // The shared URL should appear only once in deleted list.
        var deleted = fixture.Storage.Deleted;
        deleted.Should().OnlyContain(s => s != "https://storage/image-0.jpg" || deleted.Count(d => d == s) == 1);
    }

    private sealed class RecipeLifecycleFixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; }
        public TestCurrentUser User { get; }
        public Recipe Recipe { get; }
        public RecordingRecipeCache Cache { get; }
        public RecordingStorage Storage { get; }

        public RecipeLifecycleFixture(ApplicationDbContext db, TestCurrentUser user, Recipe recipe, RecordingRecipeCache cache, RecordingStorage storage)
        {
            Db = db;
            User = user;
            Recipe = recipe;
            Cache = cache;
            Storage = storage;
        }

        public static async Task<RecipeLifecycleFixture> CreateAsync(
            RecipeStatus status = RecipeStatus.Draft,
            DateTimeOffset? publishedAt = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeLifecycle_{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options);
            var user = new TestCurrentUser();
            var recipe = new Recipe
            {
                Title = "Vegetable Soup",
                Slug = "vegetable-soup",
                AuthorId = user.UserId!,
                Status = status,
                PublishedAt = publishedAt
            };

            var category = Category.Create("Soup", null);
            db.Categories.Add(category);
            recipe.CategoryId = category.Id;
            recipe.Ingredients.Add(new RecipeIngredient { Name = "Flour" });
            recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();

            return new RecipeLifecycleFixture(db, user, recipe, new RecordingRecipeCache(), new RecordingStorage());
        }

        public void AddImages(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Recipe.Images.Add(new RecipeImage
                {
                    OriginalUrl = $"https://storage/image-{i}.jpg",
                    MediumUrl = $"https://storage/image-{i}-medium.jpg",
                    ThumbnailUrl = $"https://storage/image-{i}-thumb.jpg",
                    IsPrimary = i == 0,
                    OrderIndex = i
                });
            }
        }

        public RecipeLifecycleHandler CreateHandler() =>
            new(Db, User, Cache, Storage);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId { get; } = "recipe-owner";
        public bool IsAdmin => false;
        public bool IsAuthenticated => true;
    }

    private sealed class RecordingRecipeCache : IRecipeCache
    {
        public List<string> RemovedKeys { get; } = [];
        public List<string> RemovedPrefixes { get; } = [];

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(default(T));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            RemovedPrefixes.Add(prefix);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStorage : IFileStorageService
    {
        public List<string> Deleted { get; } = [];
        public string? FailUrl { get; set; }

        public Task<FileUploadResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> DownloadAsync(string fileUrl, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string fileUrl, CancellationToken ct)
        {
            Deleted.Add(fileUrl);
            if (FailUrl is not null && string.Equals(FailUrl, fileUrl, StringComparison.Ordinal))
            {
                throw new StorageUnavailableException("Object storage is unavailable.");
            }

            return Task.CompletedTask;
        }
    }
}
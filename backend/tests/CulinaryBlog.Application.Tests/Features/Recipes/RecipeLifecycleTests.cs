using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Contracts;
using CulinaryBlog.Application.Contracts.Storage;
using CulinaryBlog.Application.Features.Recipes.Commands;
using CulinaryBlog.Domain.Entities;
using CulinaryBlog.Infrastructure.Persistence;
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
    public async Task Delete_RecipeWithTwoImages_DeletesAllSixObjectsAndSoftDeletesRecipeAndImages()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var img1 = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "https://storage/recipes/1-original.jpg",
            MediumUrl = "https://storage/recipes/1-medium.jpg",
            ThumbnailUrl = "https://storage/recipes/1-thumb.jpg",
            IsPrimary = true,
            OrderIndex = 0
        };
        var img2 = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "https://storage/recipes/2-original.jpg",
            MediumUrl = "https://storage/recipes/2-medium.jpg",
            ThumbnailUrl = "https://storage/recipes/2-thumb.jpg",
            IsPrimary = false,
            OrderIndex = 1
        };
        fixture.Db.RecipeImages.AddRange(img1, img2);
        await fixture.Db.SaveChangesAsync();

        var handler = fixture.CreateHandler();
        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        var expectedUrls = new[]
        {
            img1.OriginalUrl, img1.MediumUrl, img1.ThumbnailUrl,
            img2.OriginalUrl, img2.MediumUrl, img2.ThumbnailUrl
        };
        foreach (var url in expectedUrls)
        {
            fixture.Storage.Verify(s => s.DeleteAsync(url, It.IsAny<CancellationToken>()), Times.Once);
        }
        fixture.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(6));

        var recipe = await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.True(recipe.IsDeleted);

        var images = await fixture.Db.RecipeImages.IgnoreQueryFilters().Where(i => i.RecipeId == fixture.Recipe.Id).ToListAsync();
        Assert.Equal(2, images.Count);
        Assert.All(images, image => Assert.True(image.IsDeleted));

        Assert.Contains($"recipes:slug:{fixture.Recipe.Slug}", fixture.Cache.RemovedKeys);
        Assert.Contains("recipes:", fixture.Cache.RemovedPrefixes);
    }

    [Fact]
    public async Task Delete_RecipeWithoutImages_DoesNotCallStorageButSoftDeletesRecipeAndInvalidatesCache()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();

        var handler = fixture.CreateHandler();
        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        fixture.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var recipe = await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.True(recipe.IsDeleted);

        Assert.Contains($"recipes:slug:{fixture.Recipe.Slug}", fixture.Cache.RemovedKeys);
        Assert.Contains("recipes:", fixture.Cache.RemovedPrefixes);
    }

    [Fact]
    public async Task Delete_WhenOneObjectFails_AttemptsAllOthersThrowsAndKeepsRecipe()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var img1 = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "https://storage/recipes/1-original.jpg",
            MediumUrl = "https://storage/recipes/1-medium.jpg",
            ThumbnailUrl = "https://storage/recipes/1-thumb.jpg",
            IsPrimary = true,
            OrderIndex = 0
        };
        var img2 = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "https://storage/recipes/2-original.jpg",
            MediumUrl = "https://storage/recipes/2-medium.jpg",
            ThumbnailUrl = "https://storage/recipes/2-thumb.jpg",
            IsPrimary = false,
            OrderIndex = 1
        };
        fixture.Db.RecipeImages.AddRange(img1, img2);
        await fixture.Db.SaveChangesAsync();

        fixture.Storage
            .Setup(s => s.DeleteAsync(img2.MediumUrl, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom-medium"));

        var handler = fixture.CreateHandler();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None));
        Assert.Equal("boom-medium", ex.Message);

        var expectedUrls = new[]
        {
            img1.OriginalUrl, img1.MediumUrl, img1.ThumbnailUrl,
            img2.OriginalUrl, img2.MediumUrl, img2.ThumbnailUrl
        };
        foreach (var url in expectedUrls)
        {
            fixture.Storage.Verify(s => s.DeleteAsync(url, It.IsAny<CancellationToken>()), Times.Once);
        }
        fixture.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(6));

        var recipe = await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.False(recipe.IsDeleted);

        var images = await fixture.Db.RecipeImages.IgnoreQueryFilters().Where(i => i.RecipeId == fixture.Recipe.Id).ToListAsync();
        Assert.Equal(2, images.Count);
        Assert.All(images, image => Assert.False(image.IsDeleted));

        Assert.Contains($"recipes:slug:{fixture.Recipe.Slug}", fixture.Cache.RemovedKeys);
        Assert.Contains("recipes:", fixture.Cache.RemovedPrefixes);
    }

    [Fact]
    public async Task Delete_DeletesStorageObjectsBeforeSoftDeletingRecipe()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var img = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "https://storage/recipes/photo.jpg",
            IsPrimary = true,
            OrderIndex = 0
        };
        fixture.Db.RecipeImages.Add(img);
        await fixture.Db.SaveChangesAsync();

        var observedIsDeleted = new List<bool>();
        fixture.Storage
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((url, ct) =>
            {
                var isDeleted = fixture.Db.Recipes
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(r => r.Id == fixture.Recipe.Id)
                    .Select(r => r.IsDeleted)
                    .Single();
                observedIsDeleted.Add(isDeleted);
            })
            .Returns(Task.CompletedTask);

        var handler = fixture.CreateHandler();
        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        Assert.NotEmpty(observedIsDeleted);
        Assert.All(observedIsDeleted, isDeleted => Assert.False(isDeleted));

        var finalRecipe = await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.True(finalRecipe.IsDeleted);
    }

    [Fact]
    public async Task Delete_ByNonOwnerNonAdmin_ThrowsForbiddenAndDoesNotTouchStorage()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var img = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "https://storage/recipes/photo.jpg",
            IsPrimary = true,
            OrderIndex = 0
        };
        fixture.Db.RecipeImages.Add(img);
        await fixture.Db.SaveChangesAsync();

        fixture.User.UserId = "different-user";
        fixture.User.IsAdmin = false;

        var handler = fixture.CreateHandler();
        await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None));

        fixture.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        var recipe = await fixture.Db.Recipes.IgnoreQueryFilters().SingleAsync(r => r.Id == fixture.Recipe.Id);
        Assert.False(recipe.IsDeleted);

        var image = await fixture.Db.RecipeImages.IgnoreQueryFilters().SingleAsync(i => i.Id == img.Id);
        Assert.False(image.IsDeleted);
    }

    [Fact]
    public async Task Delete_WithDuplicateAndNullUrls_DeletesEachDistinctUrlOnce()
    {
        await using var fixture = await RecipeLifecycleFixture.CreateAsync();
        var img1 = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "u1",
            MediumUrl = "u1",
            ThumbnailUrl = null,
            IsPrimary = true,
            OrderIndex = 0
        };
        var img2 = new RecipeImage
        {
            RecipeId = fixture.Recipe.Id,
            OriginalUrl = "u2",
            MediumUrl = null,
            ThumbnailUrl = "u1",
            IsPrimary = false,
            OrderIndex = 1
        };
        fixture.Db.RecipeImages.AddRange(img1, img2);
        await fixture.Db.SaveChangesAsync();

        var handler = fixture.CreateHandler();
        await handler.Handle(new DeleteRecipeCommand(fixture.Recipe.Id), CancellationToken.None);

        fixture.Storage.Verify(s => s.DeleteAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
        fixture.Storage.Verify(s => s.DeleteAsync("u2", It.IsAny<CancellationToken>()), Times.Once);
        fixture.Storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        fixture.Storage.Verify(s => s.DeleteAsync(null!, It.IsAny<CancellationToken>()), Times.Never);
        fixture.Storage.Verify(s => s.DeleteAsync(string.Empty, It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class RecipeLifecycleFixture : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required TestCurrentUser User { get; init; }
        public required Recipe Recipe { get; init; }
        public Mock<IFileStorageService> Storage { get; } = new();
        public RecordingRecipeCache Cache { get; } = new();

        public static async Task<RecipeLifecycleFixture> CreateAsync(
            RecipeStatus status = RecipeStatus.Draft,
            DateTimeOffset? publishedAt = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"RecipeLifecycle_{Guid.NewGuid():N}").Options;
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
            recipe.Ingredients.Add(new RecipeIngredient { Name = "Flour" });
            recipe.Steps.Add(new RecipeStep { StepNumber = 1, Description = "Mix" });

            db.Recipes.Add(recipe);
            await db.SaveChangesAsync();
            return new RecipeLifecycleFixture { Db = db, User = user, Recipe = recipe };
        }

        public RecipeLifecycleHandler CreateHandler() =>
            new(Db, User, Cache, Storage.Object);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class TestCurrentUser : ICurrentUserService
    {
        public string? UserId { get; set; } = "recipe-owner";
        public bool IsAdmin { get; set; } = false;
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
}
